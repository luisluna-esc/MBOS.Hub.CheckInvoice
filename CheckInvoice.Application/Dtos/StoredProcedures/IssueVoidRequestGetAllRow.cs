using CheckInvoice.Application.Dtos.Movements;

namespace CheckInvoice.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_issue_void_requests (Scrips/storedProcedures.sql). Usada
/// únicamente por IssueVoidRequestService.GetAllVoidRequests para mapear el SqlQueryRaw del
/// procedimiento almacenado; no la uses para nada del CRUD normal de Anulaciones, que sigue
/// siendo IssueVoidRequestDto.
/// Mismos campos que IssueVoidRequestDto más el total de registros de la página.
/// </summary>
public class IssueVoidRequestGetAllRow : IssueVoidRequestDto
{
    public int TotalRecords { get; set; }
}
