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

public class DepartmentService : IDepartmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<DepartmentDto> _validator;

    public DepartmentService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<DepartmentDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllDepartments(PaginationQueryFilter paginationQueryFilter, DepartmentQueryFilter departmentQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Department>().Query();

        if (departmentQueryFilter.DepartmentId.HasValue)
        {
            query = query.Where(d => d.DepartmentId == departmentQueryFilter.DepartmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(d => d.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (departmentQueryFilter.IsActive.HasValue)
        {
            query = query.Where(d => d.IsActive == departmentQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var departments = await query
            .OrderBy(d => d.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<DepartmentDto>
            {
                Items = departments.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertDepartment(DepartmentDto departmentDto)
    {
        var validationResult = await _validator.ValidateAsync(departmentDto);
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

        var department = new Department
        {
            Name = departmentDto.Name,
            IsActive = departmentDto.IsActive
        };

        await _unitOfWork.Repository<Department>().AddAsync(department);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = department.DepartmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Department created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateDepartment(long id, DepartmentDto departmentDto)
    {
        var validationResult = await _validator.ValidateAsync(departmentDto);
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

        var repository = _unitOfWork.Repository<Department>();
        var department = await repository.GetByIdAsync(id);

        if (department is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Department not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        department.Name = departmentDto.Name;
        department.IsActive = departmentDto.IsActive;

        repository.Update(department);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = department.DepartmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Department updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteDepartment(long id)
    {
        var repository = _unitOfWork.Repository<Department>();
        var department = await repository.GetByIdAsync(id);

        if (department is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Department not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(department);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = department.DepartmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Department deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static DepartmentDto ToDto(Department department) => new()
    {
        DepartmentId = department.DepartmentId,
        Name = department.Name,
        IsActive = department.IsActive
    };
}