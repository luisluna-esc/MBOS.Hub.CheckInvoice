using System.Net;
using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class WarehouseService : IWarehouseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<WarehouseDto> _validator;

    public WarehouseService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<WarehouseDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllWarehouses(PaginationQueryFilter paginationQueryFilter, WarehouseQueryFilter warehouseQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Warehouse>().Query();

        if (warehouseQueryFilter.WarehouseId.HasValue)
        {
            query = query.Where(w => w.WarehouseId == warehouseQueryFilter.WarehouseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(w => w.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (warehouseQueryFilter.IsActive.HasValue)
        {
            query = query.Where(w => w.IsActive == warehouseQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var warehouses = await query
            .OrderBy(w => w.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<WarehouseDto>
            {
                Items = warehouses.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertWarehouse(WarehouseDto warehouseDto)
    {
        var validationResult = await _validator.ValidateAsync(warehouseDto);
        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var warehouse = new Warehouse
        {
            Name = warehouseDto.Name,
            Address = warehouseDto.Address,
            IsActive = warehouseDto.IsActive
        };

        await _unitOfWork.Repository<Warehouse>().AddAsync(warehouse);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehouse.WarehouseId,
            Messages = [new Message { Type = MessageType.Success, Description = "Warehouse created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateWarehouse(long id, WarehouseDto warehouseDto)
    {
        var validationResult = await _validator.ValidateAsync(warehouseDto);
        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Warehouse>();
        var warehouse = await repository.GetByIdAsync(id);

        if (warehouse is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Warehouse not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        warehouse.Name = warehouseDto.Name;
        warehouse.Address = warehouseDto.Address;
        warehouse.IsActive = warehouseDto.IsActive;

        repository.Update(warehouse);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehouse.WarehouseId,
            Messages = [new Message { Type = MessageType.Success, Description = "Warehouse updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteWarehouse(long id)
    {
        var repository = _unitOfWork.Repository<Warehouse>();
        var warehouse = await repository.GetByIdAsync(id);

        if (warehouse is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Warehouse not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(warehouse);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehouse.WarehouseId,
            Messages = [new Message { Type = MessageType.Success, Description = "Warehouse deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static WarehouseDto ToDto(Warehouse warehouse) => new()
    {
        WarehouseId = warehouse.WarehouseId,
        Name = warehouse.Name,
        Address = warehouse.Address,
        IsActive = warehouse.IsActive
    };
}