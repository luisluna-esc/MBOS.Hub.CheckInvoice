using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
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

public class InventoryCountService : IInventoryCountService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<InventoryCountRequestDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public InventoryCountService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<InventoryCountRequestDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllInventoryCounts(PaginationQueryFilter paginationQueryFilter, InventoryCountQueryFilter inventoryCountQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<InventoryCount>().Query();

        if (inventoryCountQueryFilter.InventoryCountId.HasValue)
        {
            query = query.Where(i => i.InventoryCountId == inventoryCountQueryFilter.InventoryCountId.Value);
        }

        if (inventoryCountQueryFilter.WarehouseId.HasValue)
        {
            query = query.Where(i => i.WarehouseId == inventoryCountQueryFilter.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(inventoryCountQueryFilter.Status))
        {
            query = query.Where(i => i.Status == inventoryCountQueryFilter.Status);
        }

        var totalRecords = await query.CountAsync();

        var inventoryCounts = await query
            .OrderByDescending(i => i.CountDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<InventoryCountDto>
            {
                Items = inventoryCounts.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetInventoryCountDetails(long inventoryCountId)
    {
        var inventoryCount = await _unitOfWork.Repository<InventoryCount>().GetByIdAsync(inventoryCountId);
        if (inventoryCount is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "InventoryCount not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var details = await _unitOfWork.Repository<InventoryCountDetail>().Query()
            .Where(d => d.InventoryCountId == inventoryCountId)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = details.Select(d => new InventoryCountDetailDto
            {
                InventoryCountDetailId = d.InventoryCountDetailId,
                InventoryCountId = d.InventoryCountId,
                ProductId = d.ProductId,
                SystemQuantity = d.SystemQuantity,
                UnitValue = d.UnitValue,
                PhysicalQuantity = d.PhysicalQuantity,
                Difference = d.Difference,
                Notes = d.Notes
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertInventoryCount(InventoryCountRequestDto inventoryCountRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(inventoryCountRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == inventoryCountRequestDto.WarehouseId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "WarehouseId does not reference an existing warehouse." });
        }

        if (inventoryCountRequestDto.SourceCountId.HasValue &&
            !await _unitOfWork.Repository<InventoryCount>().Query().AnyAsync(i => i.InventoryCountId == inventoryCountRequestDto.SourceCountId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "SourceCountId does not reference an existing inventory count." });
        }

        foreach (var line in inventoryCountRequestDto.Details)
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

        var currentAppUserId = _currentUserService.AppUserId;
        var now = DateTime.UtcNow;

        var inventoryCount = new InventoryCount
        {
            WarehouseId = inventoryCountRequestDto.WarehouseId,
            SourceCountId = inventoryCountRequestDto.SourceCountId,
            StartDate = inventoryCountRequestDto.StartDate,
            EndDate = inventoryCountRequestDto.EndDate,
            CountDate = now,
            Status = "closed",
            CreatedById = currentAppUserId,
            CreatedAt = now,
            ClosedById = currentAppUserId,
            ClosedAt = now
        };

        await _unitOfWork.Repository<InventoryCount>().AddAsync(inventoryCount);
        await _unitOfWork.SaveChangesAsync();

        var stockRepository = _unitOfWork.Repository<Stock>();

        foreach (var line in inventoryCountRequestDto.Details)
        {
            var stock = await stockRepository.Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == inventoryCountRequestDto.WarehouseId && s.ProductId == line.ProductId);

            var systemQuantity = stock?.Quantity ?? 0;
            var unitValue = stock?.AverageCost ?? 0;
            var difference = line.PhysicalQuantity - systemQuantity;

            await _unitOfWork.Repository<InventoryCountDetail>().AddAsync(new InventoryCountDetail
            {
                InventoryCountId = inventoryCount.InventoryCountId,
                ProductId = line.ProductId,
                SystemQuantity = systemQuantity,
                UnitValue = unitValue,
                PhysicalQuantity = line.PhysicalQuantity,
                Difference = difference,
                Notes = line.Notes
            });

            if (stock is null)
            {
                if (line.PhysicalQuantity > 0)
                {
                    await stockRepository.AddAsync(new Stock
                    {
                        WarehouseId = inventoryCountRequestDto.WarehouseId,
                        ProductId = line.ProductId,
                        Quantity = line.PhysicalQuantity,
                        AverageCost = 0
                    });
                }
            }
            else
            {
                stock.Quantity = line.PhysicalQuantity;
                stockRepository.Update(stock);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = inventoryCount.InventoryCountId,
            Messages = [new Message { Type = MessageType.Success, Description = "InventoryCount created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    private static InventoryCountDto ToDto(InventoryCount inventoryCount) => new()
    {
        InventoryCountId = inventoryCount.InventoryCountId,
        WarehouseId = inventoryCount.WarehouseId,
        SourceCountId = inventoryCount.SourceCountId,
        StartDate = inventoryCount.StartDate,
        EndDate = inventoryCount.EndDate,
        CountDate = inventoryCount.CountDate,
        Status = inventoryCount.Status,
        CreatedById = inventoryCount.CreatedById,
        CreatedAt = inventoryCount.CreatedAt,
        ClosedById = inventoryCount.ClosedById,
        ClosedAt = inventoryCount.ClosedAt
    };
}