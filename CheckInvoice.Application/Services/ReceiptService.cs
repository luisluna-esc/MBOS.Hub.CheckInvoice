using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class ReceiptService : IReceiptService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ReceiptRequestDto> _validator;

    public ReceiptService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ReceiptRequestDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllReceipts(PaginationQueryFilter paginationQueryFilter, ReceiptQueryFilter receiptQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Receipt>().Query();

        if (receiptQueryFilter.ReceiptId.HasValue)
        {
            query = query.Where(r => r.ReceiptId == receiptQueryFilter.ReceiptId.Value);
        }

        if (receiptQueryFilter.SupplierId.HasValue)
        {
            query = query.Where(r => r.SupplierId == receiptQueryFilter.SupplierId.Value);
        }

        if (receiptQueryFilter.WarehouseId.HasValue)
        {
            query = query.Where(r => r.WarehouseId == receiptQueryFilter.WarehouseId.Value);
        }

        if (receiptQueryFilter.ReceiptTypeId.HasValue)
        {
            query = query.Where(r => r.ReceiptTypeId == receiptQueryFilter.ReceiptTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(receiptQueryFilter.InvoiceNumber))
        {
            query = query.Where(r => r.InvoiceNumber == receiptQueryFilter.InvoiceNumber);
        }

        var totalRecords = await query.CountAsync();

        var receipts = await query
            .OrderByDescending(r => r.IssueDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ReceiptDto>
            {
                Items = receipts.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetReceiptDetails(long receiptId)
    {
        var receipt = await _unitOfWork.Repository<Receipt>().GetByIdAsync(receiptId);
        if (receipt is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Receipt not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var details = await _unitOfWork.Repository<ReceiptDetail>().Query()
            .Where(d => d.ReceiptId == receiptId)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = details.Select(d => new ReceiptDetailDto
            {
                ReceiptDetailId = d.ReceiptDetailId,
                ReceiptId = d.ReceiptId,
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitCost = d.UnitCost,
                TotalCost = d.TotalCost,
                WorkOrder = d.WorkOrder,
                Detail = d.Detail
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertReceipt(ReceiptRequestDto receiptRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(receiptRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == receiptRequestDto.WarehouseId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "WarehouseId does not reference an existing warehouse." });
        }

        if (receiptRequestDto.SupplierId.HasValue &&
            !await _unitOfWork.Repository<Supplier>().Query().AnyAsync(s => s.SupplierId == receiptRequestDto.SupplierId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "SupplierId does not reference an existing supplier." });
        }

        if (receiptRequestDto.WarehousePeriodId.HasValue)
        {
            var warehousePeriod = await _unitOfWork.Repository<WarehousePeriod>().Query()
                .FirstOrDefaultAsync(w => w.WarehousePeriodId == receiptRequestDto.WarehousePeriodId.Value);

            if (warehousePeriod is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "WarehousePeriodId does not reference an existing warehouse period." });
            }
            else if (warehousePeriod.IsClosed)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "This warehouse period is closed and cannot receive new movements." });
            }
        }

        if (receiptRequestDto.ReceiptTypeId.HasValue &&
            !await _unitOfWork.Repository<ReceiptType>().Query().AnyAsync(r => r.ReceiptTypeId == receiptRequestDto.ReceiptTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ReceiptTypeId does not reference an existing receipt type." });
        }

        foreach (var line in receiptRequestDto.Details)
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

        var receipt = new Receipt
        {
            SupplierId = receiptRequestDto.SupplierId,
            TaxId = receiptRequestDto.TaxId,
            WarehouseId = receiptRequestDto.WarehouseId,
            WarehousePeriodId = receiptRequestDto.WarehousePeriodId,
            ReceiptTypeId = receiptRequestDto.ReceiptTypeId,
            InvoiceNumber = receiptRequestDto.InvoiceNumber,
            Description = receiptRequestDto.Description,
            IssueDate = DateTime.UtcNow,
            InvoiceTotal = receiptRequestDto.InvoiceTotal
        };

        await _unitOfWork.Repository<Receipt>().AddAsync(receipt);
        await _unitOfWork.SaveChangesAsync();

        var stockCache = new Dictionary<long, Stock>();

        foreach (var line in receiptRequestDto.Details)
        {
            var totalCost = line.Quantity * line.UnitCost;

            await _unitOfWork.Repository<ReceiptDetail>().AddAsync(new ReceiptDetail
            {
                ReceiptId = receipt.ReceiptId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = line.UnitCost,
                TotalCost = totalCost,
                WorkOrder = line.WorkOrder,
                Detail = line.Detail
            });

            await IncreaseStock(stockCache, receiptRequestDto.WarehouseId, line.ProductId, line.Quantity, line.UnitCost);
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = receipt.ReceiptId,
            Messages = [new Message { Type = MessageType.Success, Description = "Receipt created successfully." }],
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

    private static ReceiptDto ToDto(Receipt receipt) => new()
    {
        ReceiptId = receipt.ReceiptId,
        SupplierId = receipt.SupplierId,
        TaxId = receipt.TaxId,
        WarehouseId = receipt.WarehouseId,
        WarehousePeriodId = receipt.WarehousePeriodId,
        ReceiptTypeId = receipt.ReceiptTypeId,
        InvoiceNumber = receipt.InvoiceNumber,
        Description = receipt.Description,
        IssueDate = receipt.IssueDate,
        InvoiceTotal = receipt.InvoiceTotal
    };
}