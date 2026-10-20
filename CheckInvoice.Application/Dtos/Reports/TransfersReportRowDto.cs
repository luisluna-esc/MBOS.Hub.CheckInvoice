namespace CheckInvoice.Application.Dtos.Reports;

public class TransfersReportRowDto
{
    public long TransferId { get; set; }
    // En hora de Bolivia.
    public DateTime TransferDate { get; set; }
    public string? SourceWarehouseName { get; set; }
    public string? DestinationWarehouseName { get; set; }
    public string? SenderName { get; set; }
    public string ReviewStatus { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? UnitMeasure { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
