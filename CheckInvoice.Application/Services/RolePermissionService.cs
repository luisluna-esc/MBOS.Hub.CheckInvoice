using System.Net;
using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class RolePermissionService : IRolePermissionService
{
    private readonly IUnitOfWork _unitOfWork;

    public RolePermissionService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ResponseGetObject> GetPermissionsByRole(long roleId)
    {
        var role = await _unitOfWork.Repository<Role>().GetByIdAsync(roleId);
        if (role is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Role not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var permissions = await (
            from rp in _unitOfWork.Repository<RolePermission>().Query()
            join p in _unitOfWork.Repository<Permission>().Query() on rp.PermissionId equals p.PermissionId
            where rp.RoleId == roleId
            orderby p.Module, p.Name
            select p
        ).ToListAsync();

        return new ResponseGetObject
        {
            Data = permissions.Select(p => new PermissionDto
            {
                PermissionId = p.PermissionId,
                Code = p.Code,
                Name = p.Name,
                Description = p.Description,
                Module = p.Module,
                IsActive = p.IsActive,
                CreatedAt = p.CreatedAt
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> AssignPermission(long roleId, long permissionId)
    {
        var role = await _unitOfWork.Repository<Role>().GetByIdAsync(roleId);
        if (role is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Role not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var permission = await _unitOfWork.Repository<Permission>().GetByIdAsync(permissionId);
        if (permission is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Permission not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var alreadyAssigned = await _unitOfWork.Repository<RolePermission>().Query()
            .AnyAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

        if (alreadyAssigned)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "This permission is already assigned to the role." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        if (permission.Code is "trabajo" or "visita")
        {
            var opposingCode = permission.Code == "trabajo" ? "visita" : "trabajo";
            var hasOpposing = await (
                from rp in _unitOfWork.Repository<RolePermission>().Query()
                join p in _unitOfWork.Repository<Permission>().Query() on rp.PermissionId equals p.PermissionId
                where rp.RoleId == roleId && p.Code == opposingCode
                select rp
            ).AnyAsync();

            if (hasOpposing)
            {
                return new ResponsePost
                {
                    Id = 0,
                    Messages = [new Message { Type = MessageType.Error, Description = "A role cannot have both 'trabajo' and 'visita' at the same time. Remove the other one first." }],
                    StatusCode = HttpStatusCode.Conflict
                };
            }
        }

        var rolePermission = new RolePermission
        {
            RoleId = roleId,
            PermissionId = permissionId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<RolePermission>().AddAsync(rolePermission);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = rolePermission.RolePermissionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Permission assigned to role successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> RemovePermission(long roleId, long permissionId)
    {
        var role = await _unitOfWork.Repository<Role>().GetByIdAsync(roleId);
        if (role?.Name == "M-BOS")
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "The M-BOS role's permissions cannot be removed by anyone." }],
                StatusCode = HttpStatusCode.Forbidden
            };
        }

        var repository = _unitOfWork.Repository<RolePermission>();
        var rolePermission = await repository.Query()
            .FirstOrDefaultAsync(rp => rp.RoleId == roleId && rp.PermissionId == permissionId);

        if (rolePermission is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "This permission is not assigned to the role." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(rolePermission);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = rolePermission.RolePermissionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Permission removed from role successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }
}