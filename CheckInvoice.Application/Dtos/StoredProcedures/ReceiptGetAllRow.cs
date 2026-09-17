using CheckInvoice.Application.Dtos.Movements;

namespace CheckInvoice.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_receipts (Scrips/storedProcedures.sql). Usada únicamente por
/// ReceiptService.GetAllReceipts para mapear el SqlQueryRaw del procedimiento almacenado; no
/// la uses para nada del CRUD normal de Entradas, que sigue siendo ReceiptDto.
/// Mismos campos que ReceiptDto más el total de registros de la página.
/// </summary>
public class ReceiptGetAllRow : ReceiptDto
{
    public int TotalRecords { get; set; }
}
