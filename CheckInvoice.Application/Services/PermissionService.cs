using System.Net;
using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Security;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class PermissionService : IPermissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<PermissionDto> _validator;

    public PermissionService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<PermissionDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllPermissions(PaginationQueryFilter paginationQueryFilter, PermissionQueryFilter permissionQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Permission>().Query();

        if (permissionQueryFilter.PermissionId.HasValue)
        {
            query = query.Where(p => p.PermissionId == permissionQueryFilter.PermissionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(permissionQueryFilter.Code))
        {
            query = query.Where(p => p.Code == permissionQueryFilter.Code);
        }

        if (!string.IsNullOrWhiteSpace(permissionQueryFilter.Module))
        {
            query = query.Where(p => p.Module == permissionQueryFilter.Module);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(p => p.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (permissionQueryFilter.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == permissionQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var permissions = await query
            .OrderBy(p => p.Module).ThenBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<PermissionDto>
            {
                Items = permissions.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertPermission(PermissionDto permissionDto)
    {
        var validationResult = await _validator.ValidateAsync(permissionDto);
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

        var permission = new Permission
        {
            Code = permissionDto.Code,
            Name = permissionDto.Name,
            Description = permissionDto.Description,
            Module = permissionDto.Module,
            IsActive = permissionDto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Permission>().AddAsync(permission);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = permission.PermissionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Permission created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdatePermission(long id, PermissionDto permissionDto)
    {
        var validationResult = await _validator.ValidateAsync(permissionDto);
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

        var repository = _unitOfWork.Repository<Permission>();
        var permission = await repository.GetByIdAsync(id);

        if (permission is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Permission not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        permission.Code = permissionDto.Code;
        permission.Name = permissionDto.Name;
        permission.Description = permissionDto.Description;
        permission.Module = permissionDto.Module;
        permission.IsActive = permissionDto.IsActive;

        repository.Update(permission);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = permission.PermissionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Permission updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeletePermission(long id)
    {
        var repository = _unitOfWork.Repository<Permission>();
        var permission = await repository.GetByIdAsync(id);

        if (permission is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Permission not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(permission);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = permission.PermissionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Permission deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static PermissionDto ToDto(Permission permission) => new()
    {
        PermissionId = permission.PermissionId,
        Code = permission.Code,
        Name = permission.Name,
        Description = permission.Description,
        Module = permission.Module,
        IsActive = permission.IsActive,
        CreatedAt = permission.CreatedAt
    };
}