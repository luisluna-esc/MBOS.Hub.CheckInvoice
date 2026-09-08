using System.Net;
using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class RoleMenuService : IRoleMenuService
{
    private readonly IUnitOfWork _unitOfWork;

    public RoleMenuService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ResponseGetObject> GetMenusByRole(long roleId)
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

        var menus = await (
            from mr in _unitOfWork.Repository<MenuRole>().Query()
            join m in _unitOfWork.Repository<Menu>().Query() on mr.MenuId equals m.MenuId
            where mr.RoleId == roleId
            orderby m.DisplayOrder, m.Name
            select m
        ).ToListAsync();

        return new ResponseGetObject
        {
            Data = menus.Select(m => new MenuDto
            {
                MenuId = m.MenuId,
                Name = m.Name,
                TranslationKey = m.TranslationKey,
                Route = m.Route,
                Icon = m.Icon,
                ParentMenuId = m.ParentMenuId,
                DisplayOrder = m.DisplayOrder,
                IsActive = m.IsActive,
                PermissionId = m.PermissionId
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> AssignMenu(long roleId, long menuId)
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

        var menu = await _unitOfWork.Repository<Menu>().GetByIdAsync(menuId);
        if (menu is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Menu not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var alreadyAssigned = await _unitOfWork.Repository<MenuRole>().Query()
            .AnyAsync(mr => mr.RoleId == roleId && mr.MenuId == menuId);

        if (alreadyAssigned)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "This menu is already assigned to the role." }],
                StatusCode = HttpStatusCode.Conflict
            };
        }

        var menuRole = new MenuRole
        {
            RoleId = roleId,
            MenuId = menuId,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<MenuRole>().AddAsync(menuRole);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = menuRole.MenuRoleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Menu assigned to role successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> RemoveMenu(long roleId, long menuId)
    {
        var repository = _unitOfWork.Repository<MenuRole>();
        var menuRole = await repository.Query()
            .FirstOrDefaultAsync(mr => mr.RoleId == roleId && mr.MenuId == menuId);

        if (menuRole is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "This menu is not assigned to the role." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(menuRole);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = menuRole.MenuRoleId,
            Messages = [new Message { Type = MessageType.Success, Description = "Menu removed from role successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }
}
