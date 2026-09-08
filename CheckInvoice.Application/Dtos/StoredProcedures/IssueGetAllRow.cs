using CheckInvoice.Application.Dtos.Movements;

namespace CheckInvoice.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_issues (Scrips/storedProcedures.sql). Usada únicamente por
/// IssueService.GetAllIssues para mapear el SqlQueryRaw del procedimiento almacenado; no la
/// uses para nada del CRUD normal de Salidas, que sigue siendo IssueDto.
/// Mismos campos que IssueDto más el total de registros de la página, calculado en la misma
/// consulta con COUNT(*) OVER() en vez de una consulta de conteo aparte.
/// </summary>
public class IssueGetAllRow : IssueDto
{
    public int TotalRecords { get; set; }
}
