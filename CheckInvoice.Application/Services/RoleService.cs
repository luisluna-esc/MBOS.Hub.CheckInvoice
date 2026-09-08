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

public class RoleService : IRoleService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<RoleDto> _validator;

    public RoleService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<RoleDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllRoles(PaginationQueryFilter paginationQueryFilter, RoleQueryFilter roleQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Role>().Query();

        if (roleQueryFilter.RoleId.HasValue)
        {
            query = query.Where(r => r.RoleId == roleQueryFilter.RoleId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(r => r.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (roleQueryFilter.IsActive.HasValue)
        {
            query = query.Where(r => r.IsActive == roleQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var roles = await query
            .OrderBy(r => r.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<RoleDto>
            {
                Items = roles.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertRole(RoleDto roleDto)
    {
        var validationResult = await _validator.ValidateAsync(roleDto);
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

        var role = new Role
        {
            Name = roleDto.Name,
            Description = roleDto.Description,
            IsActive = roleDto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<Role>().AddAsync(role);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = role.RoleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Role created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateRole(long id, RoleDto roleDto)
    {
        var validationResult = await _validator.ValidateAsync(roleDto);
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

        var repository = _unitOfWork.Repository<Role>();
        var role = await repository.GetByIdAsync(id);

        if (role is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Role not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        role.Name = roleDto.Name;
        role.Description = roleDto.Description;
        role.IsActive = roleDto.IsActive;
        role.UpdatedAt = DateTime.UtcNow;

        repository.Update(role);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = role.RoleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Role updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteRole(long id)
    {
        var repository = _unitOfWork.Repository<Role>();
        var role = await repository.GetByIdAsync(id);

        if (role is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Role not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(role);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = role.RoleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Role deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static RoleDto ToDto(Role role) => new()
    {
        RoleId = role.RoleId,
        Name = role.Name,
        Description = role.Description,
        IsActive = role.IsActive,
        CreatedAt = role.CreatedAt,
        CreatedById = role.CreatedById,
        UpdatedAt = role.UpdatedAt,
        UpdatedById = role.UpdatedById
    };
}