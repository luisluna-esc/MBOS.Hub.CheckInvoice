namespace CheckInvoice.Application.Interfaces.Security;

public interface ICurrentUserService
{
    long? AppUserId { get; }
    bool HasPermission(string code);
}