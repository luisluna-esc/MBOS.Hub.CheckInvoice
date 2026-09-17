using CheckInvoice.Application.Dtos.Movements;

namespace CheckInvoice.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_receipt_void_requests (Scrips/storedProcedures.sql). Usada
/// únicamente por ReceiptVoidRequestService.GetAllVoidRequests para mapear el SqlQueryRaw del
/// procedimiento almacenado; no la uses para nada del CRUD normal de Anulaciones de Entradas,
/// que sigue siendo ReceiptVoidRequestDto.
/// Mismos campos que ReceiptVoidRequestDto más el total de registros de la página.
/// </summary>
public class ReceiptVoidRequestGetAllRow : ReceiptVoidRequestDto
{
    public int TotalRecords { get; set; }
}
