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

public class IssueTypeService : IIssueTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<IssueTypeDto> _validator;

    public IssueTypeService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<IssueTypeDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllIssueTypes(PaginationQueryFilter paginationQueryFilter, IssueTypeQueryFilter issueTypeQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<IssueType>().Query();

        if (issueTypeQueryFilter.IssueTypeId.HasValue)
        {
            query = query.Where(i => i.IssueTypeId == issueTypeQueryFilter.IssueTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(i => i.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (issueTypeQueryFilter.IsActive.HasValue)
        {
            query = query.Where(i => i.IsActive == issueTypeQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var issueTypes = await query
            .OrderBy(i => i.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<IssueTypeDto>
            {
                Items = issueTypes.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertIssueType(IssueTypeDto issueTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(issueTypeDto);
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

        var issueType = new IssueType
        {
            Name = issueTypeDto.Name,
            IsActive = issueTypeDto.IsActive
        };

        await _unitOfWork.Repository<IssueType>().AddAsync(issueType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = issueType.IssueTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "IssueType created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateIssueType(long id, IssueTypeDto issueTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(issueTypeDto);
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

        var repository = _unitOfWork.Repository<IssueType>();
        var issueType = await repository.GetByIdAsync(id);

        if (issueType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "IssueType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        issueType.Name = issueTypeDto.Name;
        issueType.IsActive = issueTypeDto.IsActive;

        repository.Update(issueType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = issueType.IssueTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "IssueType updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteIssueType(long id)
    {
        var repository = _unitOfWork.Repository<IssueType>();
        var issueType = await repository.GetByIdAsync(id);

        if (issueType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "IssueType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(issueType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = issueType.IssueTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "IssueType deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static IssueTypeDto ToDto(IssueType issueType) => new()
    {
        IssueTypeId = issueType.IssueTypeId,
        Name = issueType.Name,
        IsActive = issueType.IsActive
    };
}