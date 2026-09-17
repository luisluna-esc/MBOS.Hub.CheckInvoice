using CheckInvoice.Application.Dtos.Parties;

namespace CheckInvoice.Application.Dtos.StoredProcedures;

/// <summary>
/// Fila del resultado de sp_get_suppliers (Scrips/storedProcedures.sql). Usada únicamente por
/// SupplierService.GetAllSuppliers para mapear el SqlQueryRaw del procedimiento almacenado;
/// no la uses para nada del CRUD normal de Proveedores, que sigue siendo SupplierDto.
/// Mismos campos que SupplierDto más el total de registros de la página.
/// </summary>
public class SupplierGetAllRow : SupplierDto
{
    public int TotalRecords { get; set; }
}
