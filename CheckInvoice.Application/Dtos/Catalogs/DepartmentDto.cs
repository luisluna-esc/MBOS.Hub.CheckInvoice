namespace CheckInvoice.Application.Dtos.Catalogs;

public class DepartmentDto
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    /// <summary>Código contable (SubAccountCode del informe para contabilidad).</summary>
    public string? Code { get; set; }
    public bool IsActive { get; set; }
}