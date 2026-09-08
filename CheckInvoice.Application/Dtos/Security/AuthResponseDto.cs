namespace CheckInvoice.Application.Dtos.Security;

public class AuthResponseDto
{
    public long AppUserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshTokenExpiresAt { get; set; }
    public IEnumerable<string> Roles { get; set; } = [];
    public IEnumerable<string> Permissions { get; set; } = [];
    public Dictionary<string, List<string>> RolePermissions { get; set; } = [];
}