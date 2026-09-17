namespace CheckInvoice.Application.Dtos.Catalogs;

public class SubDepartmentDto
{
    public long SubDepartmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}