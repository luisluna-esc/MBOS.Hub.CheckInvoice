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

public class ReceiptTypeService : IReceiptTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ReceiptTypeDto> _validator;

    public ReceiptTypeService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ReceiptTypeDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllReceiptTypes(PaginationQueryFilter paginationQueryFilter, ReceiptTypeQueryFilter receiptTypeQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<ReceiptType>().Query();

        if (receiptTypeQueryFilter.ReceiptTypeId.HasValue)
        {
            query = query.Where(r => r.ReceiptTypeId == receiptTypeQueryFilter.ReceiptTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(r => r.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (receiptTypeQueryFilter.IsActive.HasValue)
        {
            query = query.Where(r => r.IsActive == receiptTypeQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var receiptTypes = await query
            .OrderBy(r => r.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ReceiptTypeDto>
            {
                Items = receiptTypes.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertReceiptType(ReceiptTypeDto receiptTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(receiptTypeDto);
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

        var receiptType = new ReceiptType
        {
            Name = receiptTypeDto.Name,
            IsActive = receiptTypeDto.IsActive
        };

        await _unitOfWork.Repository<ReceiptType>().AddAsync(receiptType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = receiptType.ReceiptTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "ReceiptType created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateReceiptType(long id, ReceiptTypeDto receiptTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(receiptTypeDto);
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

        var repository = _unitOfWork.Repository<ReceiptType>();
        var receiptType = await repository.GetByIdAsync(id);

        if (receiptType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "ReceiptType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        receiptType.Name = receiptTypeDto.Name;
        receiptType.IsActive = receiptTypeDto.IsActive;

        repository.Update(receiptType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = receiptType.ReceiptTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "ReceiptType updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteReceiptType(long id)
    {
        var repository = _unitOfWork.Repository<ReceiptType>();
        var receiptType = await repository.GetByIdAsync(id);

        if (receiptType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "ReceiptType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(receiptType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = receiptType.ReceiptTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "ReceiptType deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ReceiptTypeDto ToDto(ReceiptType receiptType) => new()
    {
        ReceiptTypeId = receiptType.ReceiptTypeId,
        Name = receiptType.Name,
        IsActive = receiptType.IsActive
    };
}