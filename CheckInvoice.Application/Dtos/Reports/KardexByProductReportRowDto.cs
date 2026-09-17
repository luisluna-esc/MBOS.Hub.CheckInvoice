namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Una fila del reporte "Kardex por Material" (sp_get_kardex_by_product_report en
/// Scrips/storedProcedures.sql): un movimiento individual (Entrada o Salida) del producto
/// filtrado, con el saldo acumulado (cantidad/valor) después de ese movimiento. No incluye
/// Transferencias.
/// </summary>
public class KardexByProductReportRowDto
{
    public string MovementType { get; set; } = string.Empty;
    public long VoucherId { get; set; }
    public DateTime VoucherDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public decimal BalanceQuantity { get; set; }
    public decimal BalanceValue { get; set; }
}
