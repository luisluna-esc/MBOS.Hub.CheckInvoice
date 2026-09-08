using System.Net;
using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.Application.Interfaces.Security;
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

public class WarehousePeriodService : IWarehousePeriodService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<WarehousePeriodDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public WarehousePeriodService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<WarehousePeriodDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllWarehousePeriods(PaginationQueryFilter paginationQueryFilter, WarehousePeriodQueryFilter warehousePeriodQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<WarehousePeriod>().Query();

        if (warehousePeriodQueryFilter.WarehousePeriodId.HasValue)
        {
            query = query.Where(w => w.WarehousePeriodId == warehousePeriodQueryFilter.WarehousePeriodId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(w => w.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var warehousePeriods = await query
            .OrderBy(w => w.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<WarehousePeriodDto>
            {
                Items = warehousePeriods.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertWarehousePeriod(WarehousePeriodDto warehousePeriodDto)
    {
        var validationResult = await _validator.ValidateAsync(warehousePeriodDto);
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

        var warehousePeriod = new WarehousePeriod
        {
            Name = warehousePeriodDto.Name
        };

        await _unitOfWork.Repository<WarehousePeriod>().AddAsync(warehousePeriod);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehousePeriod.WarehousePeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "WarehousePeriod created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateWarehousePeriod(long id, WarehousePeriodDto warehousePeriodDto)
    {
        var validationResult = await _validator.ValidateAsync(warehousePeriodDto);
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

        var repository = _unitOfWork.Repository<WarehousePeriod>();
        var warehousePeriod = await repository.GetByIdAsync(id);

        if (warehousePeriod is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "WarehousePeriod not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        warehousePeriod.Name = warehousePeriodDto.Name;

        repository.Update(warehousePeriod);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehousePeriod.WarehousePeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "WarehousePeriod updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteWarehousePeriod(long id)
    {
        var repository = _unitOfWork.Repository<WarehousePeriod>();
        var warehousePeriod = await repository.GetByIdAsync(id);

        if (warehousePeriod is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "WarehousePeriod not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(warehousePeriod);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehousePeriod.WarehousePeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "WarehousePeriod deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> CloseWarehousePeriod(long id)
    {
        var repository = _unitOfWork.Repository<WarehousePeriod>();
        var warehousePeriod = await repository.GetByIdAsync(id);

        if (warehousePeriod is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "WarehousePeriod not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (warehousePeriod.IsClosed)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "This warehouse period is already closed." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        warehousePeriod.IsClosed = true;
        warehousePeriod.ClosedAt = DateTime.UtcNow;
        warehousePeriod.ClosedById = _currentUserService.AppUserId;

        repository.Update(warehousePeriod);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehousePeriod.WarehousePeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "WarehousePeriod closed successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static WarehousePeriodDto ToDto(WarehousePeriod warehousePeriod) => new()
    {
        WarehousePeriodId = warehousePeriod.WarehousePeriodId,
        Name = warehousePeriod.Name,
        IsClosed = warehousePeriod.IsClosed,
        ClosedAt = warehousePeriod.ClosedAt,
        ClosedById = warehousePeriod.ClosedById
    };
}