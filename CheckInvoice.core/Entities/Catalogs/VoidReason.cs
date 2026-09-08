namespace CheckInvoice.core.Entities.Catalogs;

public class VoidReason
{
    public long VoidReasonId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
