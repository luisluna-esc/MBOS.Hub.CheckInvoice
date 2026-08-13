namespace CheckInvoice.Application.Dtos.Movements;

public class ReceiptRequestDto
{
    public long? SupplierId { get; set; }
    public string? TaxId { get; set; }
    public long WarehouseId { get; set; }
    public long? WarehousePeriodId { get; set; }
    public long? ReceiptTypeId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    public decimal? InvoiceTotal { get; set; }
    public List<ReceiptDetailRequestDto> Details { get; set; } = [];
}

public class ReceiptDetailRequestDto
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? WorkOrder { get; set; }
    public string? Detail { get; set; }
}