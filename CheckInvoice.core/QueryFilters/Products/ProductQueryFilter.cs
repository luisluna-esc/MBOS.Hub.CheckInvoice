namespace CheckInvoice.core.QueryFilters.Products;

public class ProductQueryFilter
{
    public long? ProductId { get; set; }
    public string? Code { get; set; }
    public long? DepartmentId { get; set; }
    public long? SubDepartmentId { get; set; }
    public bool? IsActive { get; set; }
}