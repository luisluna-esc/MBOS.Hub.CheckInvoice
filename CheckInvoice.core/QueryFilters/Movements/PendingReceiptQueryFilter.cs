namespace CheckInvoice.core.QueryFilters.Movements;

public class PendingReceiptQueryFilter
{
    public long? WarehouseId { get; set; }
    /// <summary>"pendiente", "validado" o "anulado"; sin valor, todas.</summary>
    public string? Status { get; set; }
    /// <summary>N° de la nota ("NP 3-10-26", "3-10").</summary>
    public string? NoteNumber { get; set; }
}
