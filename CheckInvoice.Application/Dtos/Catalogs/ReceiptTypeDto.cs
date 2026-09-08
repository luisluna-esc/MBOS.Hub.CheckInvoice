namespace CheckInvoice.Application.Dtos.Catalogs;

public class ReceiptTypeDto
{
    public long ReceiptTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}