namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una línea del comprobante de impresión de una Transferencia individual
/// (sp_get_transfer_voucher_lines en Scrips/storedProcedures.sql).
/// </summary>
public class TransferVoucherLineDto
{
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? UnitMeasure { get; set; }
    public string? DepartmentName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalSalePrice { get; set; }
}
