using System.Net;
using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly JwtOptions _jwtOptions;

    public AuthService(IUnitOfWork unitOfWork, IJwtTokenGenerator jwtTokenGenerator, JwtOptions jwtOptions)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenGenerator = jwtTokenGenerator;
        _jwtOptions = jwtOptions;
    }

    public async Task<ResponseGetObject> Login(LoginDto loginDto, string? ipAddress)
    {
        var usernameOrEmail = loginDto.UsernameOrEmail.Trim().ToLower();

        var appUser = await _unitOfWork.Repository<AppUser>().Query()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == usernameOrEmail || u.Email.ToLower() == usernameOrEmail);

        if (appUser is null || !BCrypt.Net.BCrypt.Verify(loginDto.Password, appUser.PasswordHash))
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Invalid username/email or password." }],
                StatusCode = HttpStatusCode.Unauthorized
            };
        }

        if (!appUser.IsActive)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "This user is inactive." }],
                StatusCode = HttpStatusCode.Unauthorized
            };
        }

        var (roles, permissions) = await GetRolesAndPermissions(appUser.AppUserId);

        var trackedAppUser = await _unitOfWork.Repository<AppUser>().GetByIdAsync(appUser.AppUserId);
        trackedAppUser!.LastLogin = DateTime.UtcNow;
        _unitOfWork.Repository<AppUser>().Update(trackedAppUser);

        var authResponse = await IssueTokens(appUser, roles, permissions, ipAddress);

        return new ResponseGetObject
        {
            Data = authResponse,
            Messages = [new Message { Type = MessageType.Success, Description = "Login successful." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> RefreshToken(RefreshRequestDto refreshRequestDto, string? ipAddress)
    {
        var repository = _unitOfWork.Repository<RefreshToken>();
        var existingToken = await repository.Query()
            .FirstOrDefaultAsync(rt => rt.Token == refreshRequestDto.RefreshToken);

        if (existingToken is null || existingToken.IsRevoked || existingToken.ExpiresAt < DateTime.UtcNow)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Invalid or expired refresh token." }],
                StatusCode = HttpStatusCode.Unauthorized
            };
        }

        var appUser = await _unitOfWork.Repository<AppUser>().GetByIdAsync(existingToken.AppUserId);
        if (appUser is null || !appUser.IsActive)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "This user is inactive." }],
                StatusCode = HttpStatusCode.Unauthorized
            };
        }

        var trackedToken = await repository.GetByIdAsync(existingToken.RefreshTokenId);
        trackedToken!.IsRevoked = true;
        repository.Update(trackedToken);

        var (roles, permissions) = await GetRolesAndPermissions(appUser.AppUserId);
        var authResponse = await IssueTokens(appUser, roles, permissions, ipAddress);

        return new ResponseGetObject
        {
            Data = authResponse,
            Messages = [new Message { Type = MessageType.Success, Description = "Token refreshed successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> Logout(LogoutRequestDto logoutRequestDto)
    {
        var repository = _unitOfWork.Repository<RefreshToken>();
        var existingToken = await repository.Query()
            .FirstOrDefaultAsync(rt => rt.Token == logoutRequestDto.RefreshToken);

        if (existingToken is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = "Refresh token not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var trackedToken = await repository.GetByIdAsync(existingToken.RefreshTokenId);
        trackedToken!.IsRevoked = true;
        repository.Update(trackedToken);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = existingToken.RefreshTokenId,
            Messages = [new Message { Type = MessageType.Success, Description = "Logout successful." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<(List<string> Roles, List<string> Permissions)> GetRolesAndPermissions(long appUserId)
    {
        var roles = await (
            from ur in _unitOfWork.Repository<AppUserRole>().Query()
            join r in _unitOfWork.Repository<Role>().Query() on ur.RoleId equals r.RoleId
            where ur.AppUserId == appUserId && r.IsActive
            select r.Name
        ).Distinct().ToListAsync();

        var permissions = await (
            from ur in _unitOfWork.Repository<AppUserRole>().Query()
            join rp in _unitOfWork.Repository<RolePermission>().Query() on ur.RoleId equals rp.RoleId
            join p in _unitOfWork.Repository<Permission>().Query() on rp.PermissionId equals p.PermissionId
            where ur.AppUserId == appUserId && p.IsActive
            select p.Code
        ).Distinct().ToListAsync();

        return (roles, permissions);
    }

    private async Task<AuthResponseDto> IssueTokens(AppUser appUser, List<string> roles, List<string> permissions, string? ipAddress)
    {
        var (accessToken, accessTokenExpiresAt) = _jwtTokenGenerator.GenerateAccessToken(appUser, roles, permissions);
        var refreshTokenValue = _jwtTokenGenerator.GenerateRefreshToken();
        var refreshTokenExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        var refreshToken = new RefreshToken
        {
            AppUserId = appUser.AppUserId,
            Token = refreshTokenValue,
            ExpiresAt = refreshTokenExpiresAt,
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
            CreatedIp = ipAddress
        };

        await _unitOfWork.Repository<RefreshToken>().AddAsync(refreshToken);
        await _unitOfWork.SaveChangesAsync();

        return new AuthResponseDto
        {
            AppUserId = appUser.AppUserId,
            Username = appUser.Username,
            Email = appUser.Email,
            AccessToken = accessToken,
            AccessTokenExpiresAt = accessTokenExpiresAt,
            RefreshToken = refreshTokenValue,
            RefreshTokenExpiresAt = refreshTokenExpiresAt,
            Roles = roles,
            Permissions = permissions
        };
    }
}