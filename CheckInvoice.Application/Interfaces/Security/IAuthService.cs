using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IAuthService
{
    Task<ResponseGetObject> Login(LoginDto loginDto, string? ipAddress);
    Task<ResponseGetObject> RefreshToken(RefreshRequestDto refreshRequestDto, string? ipAddress);
    Task<ResponsePost> Logout(LogoutRequestDto logoutRequestDto);
}