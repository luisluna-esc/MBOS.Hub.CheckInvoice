using CheckInvoice.core.Entities.Security;

namespace CheckInvoice.Application.Interfaces.Security;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) GenerateAccessToken(AppUser appUser, IEnumerable<string> roles, IEnumerable<string> permissions);
    string GenerateRefreshToken();
}