namespace CheckInvoice.Application.Dtos.Movements;

public class ReceiptVoidRequestCreateDto
{
    public long ReceiptId { get; set; }
    public long VoidReasonId { get; set; }
    public string? Detail { get; set; }
}
