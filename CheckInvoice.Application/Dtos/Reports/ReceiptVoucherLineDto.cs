namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una línea del comprobante de impresión de una Entrada individual
/// (sp_get_receipt_voucher_lines en Scrips/storedProcedures.sql).
/// </summary>
public class ReceiptVoucherLineDto
{
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnitMeasure { get; set; }
    public string? DepartmentName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
}
