namespace CheckInvoice.core.QueryFilters.Movements;

public class ReceiptVoidRequestQueryFilter
{
    public long? ReceiptVoidRequestId { get; set; }
    public long? ReceiptId { get; set; }
    public string? Status { get; set; }
    public long? RequestedBy { get; set; }
    public long? VoidReasonId { get; set; }
    /// <summary>Búsqueda parcial por nombre y apellido de quien pidió la anulación.</summary>
    public string? RequestedByName { get; set; }
    /// <summary>Rango sobre la fecha de la solicitud; DateTo es inclusivo.</summary>
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
}
