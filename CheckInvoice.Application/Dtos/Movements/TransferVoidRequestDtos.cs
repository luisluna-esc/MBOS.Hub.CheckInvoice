namespace CheckInvoice.Application.Dtos.Movements;

public class TransferVoidRequestCreateDto
{
    public long TransferId { get; set; }
    public long VoidReasonId { get; set; }
    public string? Detail { get; set; }
}

public class TransferVoidRequestDto
{
    public long TransferVoidRequestId { get; set; }
    public long TransferId { get; set; }
    public DateTime TransferDate { get; set; }
    public string? SourceWarehouseName { get; set; }
    public string? DestinationWarehouseName { get; set; }
    public decimal Total { get; set; }
    public string? VoidReasonName { get; set; }
    public string? Detail { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RequestedByName { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
}

public class TransferVoidReviewDto
{
    public string? ReviewNotes { get; set; }
}
