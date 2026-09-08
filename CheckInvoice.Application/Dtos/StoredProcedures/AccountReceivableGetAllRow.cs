using CheckInvoice.Application.Dtos.Finance;

namespace CheckInvoice.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_account_receivables (Scrips/storedProcedures.sql). Usada
/// únicamente por AccountReceivableService.GetAllAccountReceivables para mapear el
/// SqlQueryRaw del procedimiento almacenado; no la uses para nada del CRUD normal de
/// Cuentas por Cobrar, que sigue siendo AccountReceivableDto.
/// Mismos campos que AccountReceivableDto más el total de registros de la página.
/// </summary>
public class AccountReceivableGetAllRow : AccountReceivableDto
{
    public int TotalRecords { get; set; }
}
