namespace CheckInvoice.Application.Services;

// Compras: con factura el costo unitario es el precio; con retención (sin factura) el costo
// unitario es el precio por 0,92. Los nombres son los de la tabla receipt_type.
internal static class ReceiptCosting
{
    public const string PurchaseWithInvoice = "Compras con Factura";
    public const string PurchaseWithRetention = "Compras con Retencion";
    public const decimal RetentionCostFactor = 0.92m;

    // Entradas sin proveedor que también usan este endpoint: el Ajuste de Inventario (stock que
    // sube tras un conteo) y la carga del Inventario Inicial. El costo es el que se envía.
    public const string Adjustment = "Ajuste";
    public const string InitialInventory = "Inventario Inicial";
    // Donación: quien dona se elige del catálogo de proveedores; el valor es el que se estima.
    public const string Donation = "Donacion";

    public static bool IsPurchaseType(string? receiptTypeName) =>
        receiptTypeName is PurchaseWithInvoice or PurchaseWithRetention;

    public static bool IsDirectType(string? receiptTypeName) =>
        receiptTypeName is Adjustment or InitialInventory or Donation;

    public static decimal UnitCost(decimal unitPrice, bool withRetention) =>
        withRetention ? Math.Round(unitPrice * RetentionCostFactor, 2, MidpointRounding.AwayFromZero) : unitPrice;

    public static decimal TotalCost(decimal quantity, decimal unitPrice, bool withRetention) =>
        withRetention
            ? Math.Round(quantity * unitPrice * RetentionCostFactor, 2, MidpointRounding.AwayFromZero)
            : quantity * unitPrice;
}
