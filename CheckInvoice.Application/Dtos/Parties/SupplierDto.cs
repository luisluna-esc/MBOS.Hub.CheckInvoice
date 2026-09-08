namespace CheckInvoice.Application.Dtos.Parties;

public class SupplierDto
{
    public long SupplierId { get; set; }
    /// <summary>Si se envía al crear, el proveedor comparte esta identidad (Nombre/NIT/Correo/Teléfono)
    /// con un Cliente ya existente, en vez de crear una identidad nueva.</summary>
    public long? PartyId { get; set; }
    public string? Code { get; set; }
    public string? LegalName { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? TaxId { get; set; }
    public long? CountryId { get; set; }
    public string? Address { get; set; }
    public string? Phone { get; set; }
    public string? MobilePhone { get; set; }
    public string? Email { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    /// <summary>Solo de lectura: si esta misma identidad ya está registrada como Cliente.</summary>
    public long? LinkedClientId { get; set; }
    public string? LinkedClientName { get; set; }
}
