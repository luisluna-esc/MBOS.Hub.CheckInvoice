namespace CheckInvoice.core.Entities.Security;

public class RefreshToken
{
    public long RefreshTokenId { get; set; }
    public long AppUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedIp { get; set; }
}