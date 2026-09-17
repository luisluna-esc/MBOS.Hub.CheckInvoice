namespace CheckInvoice.core.Entities.Catalogs;

public class SubDepartment
{
    public long SubDepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}