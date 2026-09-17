namespace CheckInvoice.core.Entities.Catalogs;

public class ReceiptType
{
    public long ReceiptTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}