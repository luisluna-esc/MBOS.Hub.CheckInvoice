using CheckInvoice.Application.Dtos.Parties;

namespace CheckInvoice.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_clients (Scrips/storedProcedures.sql). Usada únicamente por
/// ClientService.GetAllClients para mapear el SqlQueryRaw del procedimiento almacenado; no la
/// uses para nada del CRUD normal de Clientes, que sigue siendo ClientDto.
/// Mismos campos que ClientDto más el total de registros de la página.
/// </summary>
public class ClientGetAllRow : ClientDto
{
    public int TotalRecords { get; set; }
}
