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

public class MenuService : IMenuService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<MenuDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public MenuService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<MenuDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllMenus(PaginationQueryFilter paginationQueryFilter, MenuQueryFilter menuQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Menu>().Query();

        if (menuQueryFilter.MenuId.HasValue)
        {
            query = query.Where(m => m.MenuId == menuQueryFilter.MenuId.Value);
        }

        if (menuQueryFilter.ParentMenuId.HasValue)
        {
            query = query.Where(m => m.ParentMenuId == menuQueryFilter.ParentMenuId.Value);
        }

        if (menuQueryFilter.PermissionId.HasValue)
        {
            query = query.Where(m => m.PermissionId == menuQueryFilter.PermissionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(m => m.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (menuQueryFilter.IsActive.HasValue)
        {
            query = query.Where(m => m.IsActive == menuQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var menus = await query
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<MenuDto>
            {
                Items = menus.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertMenu(MenuDto menuDto)
    {
        var validationResult = await _validator.ValidateAsync(menuDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (menuDto.ParentMenuId.HasValue &&
            !await _unitOfWork.Repository<Menu>().Query().AnyAsync(m => m.MenuId == menuDto.ParentMenuId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ParentMenuId does not reference an existing menu." });
        }

        if (menuDto.PermissionId.HasValue &&
            !await _unitOfWork.Repository<Permission>().Query().AnyAsync(p => p.PermissionId == menuDto.PermissionId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "PermissionId does not reference an existing permission." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var menu = new Menu
        {
            Name = menuDto.Name,
            TranslationKey = menuDto.TranslationKey,
            Route = menuDto.Route,
            Icon = menuDto.Icon,
            ParentMenuId = menuDto.ParentMenuId,
            DisplayOrder = menuDto.DisplayOrder,
            IsActive = menuDto.IsActive,
            PermissionId = menuDto.PermissionId
        };

        await _unitOfWork.Repository<Menu>().AddAsync(menu);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = menu.MenuId,
            Messages = [new Message { Type = MessageType.Success, Description = "Menu created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateMenu(long id, MenuDto menuDto)
    {
        var validationResult = await _validator.ValidateAsync(menuDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (menuDto.ParentMenuId.HasValue && menuDto.ParentMenuId.Value == id)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "A menu cannot be its own parent." });
        }
        else if (menuDto.ParentMenuId.HasValue &&
            !await _unitOfWork.Repository<Menu>().Query().AnyAsync(m => m.MenuId == menuDto.ParentMenuId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ParentMenuId does not reference an existing menu." });
        }

        if (menuDto.PermissionId.HasValue &&
            !await _unitOfWork.Repository<Permission>().Query().AnyAsync(p => p.PermissionId == menuDto.PermissionId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "PermissionId does not reference an existing permission." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Menu>();
        var menu = await repository.GetByIdAsync(id);

        if (menu is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Menu not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        menu.Name = menuDto.Name;
        menu.TranslationKey = menuDto.TranslationKey;
        menu.Route = menuDto.Route;
        menu.Icon = menuDto.Icon;
        menu.ParentMenuId = menuDto.ParentMenuId;
        menu.DisplayOrder = menuDto.DisplayOrder;
        menu.IsActive = menuDto.IsActive;
        menu.PermissionId = menuDto.PermissionId;

        repository.Update(menu);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = menu.MenuId,
            Messages = [new Message { Type = MessageType.Success, Description = "Menu updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteMenu(long id)
    {
        var repository = _unitOfWork.Repository<Menu>();
        var menu = await repository.GetByIdAsync(id);

        if (menu is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Menu not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(menu);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = menu.MenuId,
            Messages = [new Message { Type = MessageType.Success, Description = "Menu deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetMyMenu()
    {
        var appUserId = _currentUserService.AppUserId;

        var permissionIds = await (
            from ur in _unitOfWork.Repository<AppUserRole>().Query()
            join r in _unitOfWork.Repository<Role>().Query() on ur.RoleId equals r.RoleId
            join rp in _unitOfWork.Repository<RolePermission>().Query() on r.RoleId equals rp.RoleId
            where ur.AppUserId == appUserId && r.IsActive
            select rp.PermissionId
        ).Distinct().ToListAsync();

        var roleIds = await (
            from ur in _unitOfWork.Repository<AppUserRole>().Query()
            join r in _unitOfWork.Repository<Role>().Query() on ur.RoleId equals r.RoleId
            where ur.AppUserId == appUserId && r.IsActive
            select r.RoleId
        ).Distinct().ToListAsync();

        // Un menu es visible para un rol solo si existe una fila en menu_role para ese
        // par (menu, rol). Un menu nuevo sin ninguna fila no lo ve nadie hasta que se
        // le asignen roles explícitamente.
        var allowedMenuIdsForMyRoles = await _unitOfWork.Repository<MenuRole>().Query()
            .Where(mr => roleIds.Contains(mr.RoleId))
            .Select(mr => mr.MenuId)
            .Distinct()
            .ToListAsync();

        var allMenus = await _unitOfWork.Repository<Menu>().Query()
            .Where(m => m.IsActive && (m.PermissionId == null || permissionIds.Contains(m.PermissionId.Value)))
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Name)
            .ToListAsync();

        var visibleByRole = allMenus
            .Where(m => allowedMenuIdsForMyRoles.Contains(m.MenuId))
            .ToList();

        // Un grupo padre (sin ruta propia) no tiene sentido mostrarlo si ninguno
        // de sus hijos quedó visible tras el filtro de rol.
        var visibleChildCountByParent = visibleByRole
            .Where(m => m.ParentMenuId.HasValue)
            .GroupBy(m => m.ParentMenuId!.Value)
            .ToDictionary(g => g.Key, g => g.Count());

        var menus = visibleByRole
            .Where(m => m.Route != null || visibleChildCountByParent.GetValueOrDefault(m.MenuId) > 0)
            .ToList();

        var menuPermissionIds = menus
            .Where(m => m.PermissionId.HasValue)
            .Select(m => m.PermissionId!.Value)
            .Distinct()
            .ToList();

        var permissionCodesById = await _unitOfWork.Repository<Permission>().Query()
            .Where(p => menuPermissionIds.Contains(p.PermissionId))
            .ToDictionaryAsync(p => p.PermissionId, p => p.Code);

        var itemsById = menus.ToDictionary(m => m.MenuId, m => new MenuTreeItemDto
        {
            MenuId = m.MenuId,
            Name = m.Name,
            TranslationKey = m.TranslationKey,
            Route = m.Route,
            Icon = m.Icon,
            DisplayOrder = m.DisplayOrder,
            PermissionCode = m.PermissionId.HasValue && permissionCodesById.TryGetValue(m.PermissionId.Value, out var code)
                ? code
                : null
        });

        var roots = new List<MenuTreeItemDto>();

        foreach (var menu in menus)
        {
            var item = itemsById[menu.MenuId];

            if (menu.ParentMenuId.HasValue && itemsById.TryGetValue(menu.ParentMenuId.Value, out var parent))
            {
                parent.Children.Add(item);
            }
            else
            {
                roots.Add(item);
            }
        }

        return new ResponseGetObject
        {
            Data = roots,
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static MenuDto ToDto(Menu menu) => new()
    {
        MenuId = menu.MenuId,
        Name = menu.Name,
        TranslationKey = menu.TranslationKey,
        Route = menu.Route,
        Icon = menu.Icon,
        ParentMenuId = menu.ParentMenuId,
        DisplayOrder = menu.DisplayOrder,
        IsActive = menu.IsActive,
        PermissionId = menu.PermissionId
    };
}