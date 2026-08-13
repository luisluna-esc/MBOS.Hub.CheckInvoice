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

public class AppUserService : IAppUserService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<AppUserDto> _validator;

    public AppUserService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<AppUserDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllAppUsers(PaginationQueryFilter paginationQueryFilter, AppUserQueryFilter appUserQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<AppUser>().Query();

        if (appUserQueryFilter.AppUserId.HasValue)
        {
            query = query.Where(u => u.AppUserId == appUserQueryFilter.AppUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(appUserQueryFilter.FirstName))
        {
            query = query.Where(u => u.FirstName == appUserQueryFilter.FirstName);
        }

        if (!string.IsNullOrWhiteSpace(appUserQueryFilter.LastName))
        {
            query = query.Where(u => u.LastName == appUserQueryFilter.LastName);
        }

        if (!string.IsNullOrWhiteSpace(appUserQueryFilter.Email))
        {
            query = query.Where(u => u.Email == appUserQueryFilter.Email);
        }

        if (!string.IsNullOrWhiteSpace(appUserQueryFilter.Username))
        {
            query = query.Where(u => u.Username == appUserQueryFilter.Username);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            var searchCriteria = paginationQueryFilter.SearchCriteria;
            query = query.Where(u =>
                u.FirstName.Contains(searchCriteria) ||
                u.LastName.Contains(searchCriteria) ||
                u.Email.Contains(searchCriteria) ||
                u.Username.Contains(searchCriteria));
        }

        if (appUserQueryFilter.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == appUserQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var appUsers = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<AppUserDto>
            {
                Items = appUsers.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertAppUser(AppUserDto appUserDto)
    {
        var validationResult = await _validator.ValidateAsync(appUserDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (string.IsNullOrWhiteSpace(appUserDto.Password))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "Password is required." });
        }

        var repository = _unitOfWork.Repository<AppUser>();

        if (await repository.Query().AnyAsync(u => u.Email == appUserDto.Email))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This email is already in use." });
        }

        if (await repository.Query().AnyAsync(u => u.Username == appUserDto.Username))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This username is already in use." });
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

        var appUser = new AppUser
        {
            FirstName = appUserDto.FirstName,
            LastName = appUserDto.LastName,
            Email = appUserDto.Email,
            Username = appUserDto.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(appUserDto.Password),
            IsActive = appUserDto.IsActive,
            CreatedAt = DateTime.UtcNow
        };

        await repository.AddAsync(appUser);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "User created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateAppUser(long id, AppUserDto appUserDto)
    {
        var validationResult = await _validator.ValidateAsync(appUserDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        var repository = _unitOfWork.Repository<AppUser>();

        if (await repository.Query().AnyAsync(u => u.Email == appUserDto.Email && u.AppUserId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This email is already in use." });
        }

        if (await repository.Query().AnyAsync(u => u.Username == appUserDto.Username && u.AppUserId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This username is already in use." });
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

        var appUser = await repository.GetByIdAsync(id);

        if (appUser is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "User not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        appUser.FirstName = appUserDto.FirstName;
        appUser.LastName = appUserDto.LastName;
        appUser.Email = appUserDto.Email;
        appUser.Username = appUserDto.Username;
        appUser.IsActive = appUserDto.IsActive;
        appUser.UpdatedAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(appUserDto.Password))
        {
            appUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(appUserDto.Password);
        }

        repository.Update(appUser);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "User updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteAppUser(long id)
    {
        var repository = _unitOfWork.Repository<AppUser>();
        var appUser = await repository.GetByIdAsync(id);

        if (appUser is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "User not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(appUser);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = appUser.AppUserId,
            Messages = [new Message { Type = MessageType.Success, Description = "User deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static AppUserDto ToDto(AppUser appUser) => new()
    {
        AppUserId = appUser.AppUserId,
        FirstName = appUser.FirstName,
        LastName = appUser.LastName,
        Email = appUser.Email,
        Username = appUser.Username,
        LastLogin = appUser.LastLogin,
        IsActive = appUser.IsActive,
        CreatedAt = appUser.CreatedAt,
        CreatedById = appUser.CreatedById,
        UpdatedAt = appUser.UpdatedAt,
        UpdatedById = appUser.UpdatedById
    };
}