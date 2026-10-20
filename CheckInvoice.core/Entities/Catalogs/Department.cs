namespace CheckInvoice.core.Entities.Catalogs;

public class Department
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Código contable (SubAccountCode en el informe para contabilidad).</summary>
    public string? Code { get; set; }
    public bool IsActive { get; set; } = true;
}