namespace CheckInvoice.core.Entities.Catalogs;

public class Department
{
    public long DepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}