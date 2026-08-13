namespace CheckInvoice.Application.Dtos.Security;

public class RefreshTokenDto
{
    public long RefreshTokenId { get; set; }
    public long AppUserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedIp { get; set; }
}