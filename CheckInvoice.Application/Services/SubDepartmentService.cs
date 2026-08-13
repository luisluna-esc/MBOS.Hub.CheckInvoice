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

public class SubDepartmentService : ISubDepartmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<SubDepartmentDto> _validator;

    public SubDepartmentService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<SubDepartmentDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllSubDepartments(PaginationQueryFilter paginationQueryFilter, SubDepartmentQueryFilter subDepartmentQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<SubDepartment>().Query();

        if (subDepartmentQueryFilter.SubDepartmentId.HasValue)
        {
            query = query.Where(s => s.SubDepartmentId == subDepartmentQueryFilter.SubDepartmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(s => s.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (subDepartmentQueryFilter.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == subDepartmentQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var subDepartments = await query
            .OrderBy(s => s.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<SubDepartmentDto>
            {
                Items = subDepartments.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertSubDepartment(SubDepartmentDto subDepartmentDto)
    {
        var validationResult = await _validator.ValidateAsync(subDepartmentDto);
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

        var subDepartment = new SubDepartment
        {
            Name = subDepartmentDto.Name,
            IsActive = subDepartmentDto.IsActive
        };

        await _unitOfWork.Repository<SubDepartment>().AddAsync(subDepartment);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = subDepartment.SubDepartmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "SubDepartment created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateSubDepartment(long id, SubDepartmentDto subDepartmentDto)
    {
        var validationResult = await _validator.ValidateAsync(subDepartmentDto);
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

        var repository = _unitOfWork.Repository<SubDepartment>();
        var subDepartment = await repository.GetByIdAsync(id);

        if (subDepartment is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "SubDepartment not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        subDepartment.Name = subDepartmentDto.Name;
        subDepartment.IsActive = subDepartmentDto.IsActive;

        repository.Update(subDepartment);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = subDepartment.SubDepartmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "SubDepartment updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteSubDepartment(long id)
    {
        var repository = _unitOfWork.Repository<SubDepartment>();
        var subDepartment = await repository.GetByIdAsync(id);

        if (subDepartment is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "SubDepartment not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(subDepartment);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = subDepartment.SubDepartmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "SubDepartment deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static SubDepartmentDto ToDto(SubDepartment subDepartment) => new()
    {
        SubDepartmentId = subDepartment.SubDepartmentId,
        Name = subDepartment.Name,
        IsActive = subDepartment.IsActive
    };
}