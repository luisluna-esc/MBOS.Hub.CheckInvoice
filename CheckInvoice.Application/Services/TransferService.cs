using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class TransferService : ITransferService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<TransferRequestDto> _validator;

    public TransferService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<TransferRequestDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllTransfers(PaginationQueryFilter paginationQueryFilter, TransferQueryFilter transferQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Transfer>().Query();

        if (transferQueryFilter.TransferId.HasValue)
        {
            query = query.Where(t => t.TransferId == transferQueryFilter.TransferId.Value);
        }

        if (transferQueryFilter.SourceWarehouseId.HasValue)
        {
            query = query.Where(t => t.SourceWarehouseId == transferQueryFilter.SourceWarehouseId.Value);
        }

        if (transferQueryFilter.DestinationWarehouseId.HasValue)
        {
            query = query.Where(t => t.DestinationWarehouseId == transferQueryFilter.DestinationWarehouseId.Value);
        }

        var totalRecords = await query.CountAsync();

        var transfers = await query
            .OrderByDescending(t => t.TransferDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<TransferDto>
            {
                Items = transfers.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetTransferDetails(long transferId)
    {
        var transfer = await _unitOfWork.Repository<Transfer>().GetByIdAsync(transferId);
        if (transfer is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Transfer not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var details = await _unitOfWork.Repository<TransferDetail>().Query()
            .Where(d => d.TransferId == transferId)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = details.Select(d => new TransferDetailDto
            {
                TransferDetailId = d.TransferDetailId,
                TransferId = d.TransferId,
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice,
                TotalSalePrice = d.TotalSalePrice
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertTransfer(TransferRequestDto transferRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(transferRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == transferRequestDto.SourceWarehouseId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "SourceWarehouseId does not reference an existing warehouse." });
        }

        if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == transferRequestDto.DestinationWarehouseId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "DestinationWarehouseId does not reference an existing warehouse." });
        }

        if (transferRequestDto.SenderUserId.HasValue &&
            !await _unitOfWork.Repository<AppUser>().Query().AnyAsync(u => u.AppUserId == transferRequestDto.SenderUserId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "SenderUserId does not reference an existing user." });
        }

        if (transferRequestDto.ReceiverUserId.HasValue &&
            !await _unitOfWork.Repository<AppUser>().Query().AnyAsync(u => u.AppUserId == transferRequestDto.ReceiverUserId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ReceiverUserId does not reference an existing user." });
        }

        foreach (var line in transferRequestDto.Details)
        {
            if (!await _unitOfWork.Repository<Product>().Query().AnyAsync(p => p.ProductId == line.ProductId))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"ProductId {line.ProductId} does not reference an existing product." });
            }
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var requestedByProduct = transferRequestDto.Details
            .GroupBy(d => d.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

        var sourceStockByProduct = new Dictionary<long, Stock>();
        foreach (var productId in requestedByProduct.Keys)
        {
            var stock = await _unitOfWork.Repository<Stock>().Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == transferRequestDto.SourceWarehouseId && s.ProductId == productId);

            var available = stock?.Quantity ?? 0;
            var requested = requestedByProduct[productId];

            if (available < requested)
            {
                errors.Add(new Message
                {
                    Type = MessageType.Error,
                    Description = $"Insufficient stock for ProductId {productId}: {available} available, {requested} requested."
                });
                continue;
            }

            sourceStockByProduct[productId] = stock!;
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var transfer = new Transfer
        {
            SourceWarehouseId = transferRequestDto.SourceWarehouseId,
            DestinationWarehouseId = transferRequestDto.DestinationWarehouseId,
            SenderUserId = transferRequestDto.SenderUserId,
            ReceiverUserId = transferRequestDto.ReceiverUserId,
            TransferDate = DateTime.UtcNow,
            Notes = transferRequestDto.Notes
        };

        await _unitOfWork.Repository<Transfer>().AddAsync(transfer);
        await _unitOfWork.SaveChangesAsync();

        var stockRepository = _unitOfWork.Repository<Stock>();
        var destinationStockCache = new Dictionary<long, Stock>();

        foreach (var line in transferRequestDto.Details)
        {
            var sourceStock = sourceStockByProduct[line.ProductId];
            var movedCost = sourceStock.AverageCost;

            await _unitOfWork.Repository<TransferDetail>().AddAsync(new TransferDetail
            {
                TransferId = transfer.TransferId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                TotalSalePrice = line.UnitPrice.HasValue ? line.Quantity * line.UnitPrice.Value : null
            });

            sourceStock.Quantity -= line.Quantity;

            await IncreaseStock(destinationStockCache, transferRequestDto.DestinationWarehouseId, line.ProductId, line.Quantity, movedCost);
        }

        foreach (var stock in sourceStockByProduct.Values)
        {
            stockRepository.Update(stock);
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = transfer.TransferId,
            Messages = [new Message { Type = MessageType.Success, Description = "Transfer created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    // Reuses a per-request in-memory cache so multiple lines for the same product
    // accumulate onto one Stock instance instead of racing separate DB lookups
    // (which would otherwise insert duplicate rows and violate the unique constraint).
    private async Task IncreaseStock(Dictionary<long, Stock> stockCache, long warehouseId, long productId, decimal quantity, decimal unitCost)
    {
        if (stockCache.TryGetValue(productId, out var cachedStock))
        {
            var cachedNewQuantity = cachedStock.Quantity + quantity;
            cachedStock.AverageCost = ((cachedStock.Quantity * cachedStock.AverageCost) + (quantity * unitCost)) / cachedNewQuantity;
            cachedStock.Quantity = cachedNewQuantity;
            return;
        }

        var repository = _unitOfWork.Repository<Stock>();
        var stock = await repository.Query()
            .FirstOrDefaultAsync(s => s.WarehouseId == warehouseId && s.ProductId == productId);

        if (stock is null)
        {
            stock = new Stock
            {
                WarehouseId = warehouseId,
                ProductId = productId,
                Quantity = quantity,
                AverageCost = unitCost
            };
            await repository.AddAsync(stock);
            stockCache[productId] = stock;
            return;
        }

        var newQuantity = stock.Quantity + quantity;
        stock.AverageCost = ((stock.Quantity * stock.AverageCost) + (quantity * unitCost)) / newQuantity;
        stock.Quantity = newQuantity;
        repository.Update(stock);
        stockCache[productId] = stock;
    }

    private static TransferDto ToDto(Transfer transfer) => new()
    {
        TransferId = transfer.TransferId,
        SourceWarehouseId = transfer.SourceWarehouseId,
        DestinationWarehouseId = transfer.DestinationWarehouseId,
        SenderUserId = transfer.SenderUserId,
        ReceiverUserId = transfer.ReceiverUserId,
        TransferDate = transfer.TransferDate,
        Notes = transfer.Notes
    };
}