namespace CheckInvoice.Application.Dtos.Parties;

public class ClientDto
{
    public long ClientId { get; set; }
    /// <summary>Si se envía al crear, el cliente comparte esta identidad (Nombre/NIT/Correo/Teléfono)
    /// con un Proveedor ya existente, en vez de crear una identidad nueva.</summary>
    public long? PartyId { get; set; }
    public long? AppUserId { get; set; }
    public long? DocumentTypeId { get; set; }
    public string? TaxId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? MobilePhone { get; set; }
    public long? DistrictId { get; set; }
    public long? ChurchId { get; set; }
    public bool IsActive { get; set; }
    public string? Complement { get; set; }
    public long? SpecialCaseId { get; set; }
    public bool IsPastor { get; set; }
    /// <summary>Solo de lectura: si esta misma identidad ya está registrada como Proveedor.</summary>
    public long? LinkedSupplierId { get; set; }
    public string? LinkedSupplierName { get; set; }
}
