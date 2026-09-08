namespace CheckInvoice.core.Entities.Parties;

/// <summary>
/// Identidad compartida (Nombre, NIT, Correo, Teléfono) entre un Cliente y un
/// Proveedor cuando son la misma persona/empresa. Cliente y Proveedor apuntan
/// a la misma fila de Party cuando están vinculados; si no, cada uno tiene la suya.
/// </summary>
public class Party
{
    public long PartyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public string? Email { get; set; }
    public string? MobilePhone { get; set; }
}
