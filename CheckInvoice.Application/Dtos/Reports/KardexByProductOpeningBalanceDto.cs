namespace CheckInvoice.Application.Dtos.Reports;

/// <summary>
/// Saldo inicial del "Kardex por Material" (sp_get_kardex_by_product_opening_balance):
/// neto Entradas − Salidas del producto en el almacén, antes de la fecha de inicio filtrada.
/// </summary>
public class KardexByProductOpeningBalanceDto
{
    public decimal OpeningQuantity { get; set; }
    public decimal OpeningValue { get; set; }
}
