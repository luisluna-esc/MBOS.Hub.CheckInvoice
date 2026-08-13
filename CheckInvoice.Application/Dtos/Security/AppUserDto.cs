namespace CheckInvoice.Application.Dtos.Security;

public class AppUserDto
{
    public long AppUserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;

    // Write-only: plain text password for insert/update, hashed by AppUserService before persisting.
    // Never populated when mapping back from the entity, so it never appears in a GET response.
    public string? Password { get; set; }
    public DateTime? LastLogin { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public long? CreatedById { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public long? UpdatedById { get; set; }
}