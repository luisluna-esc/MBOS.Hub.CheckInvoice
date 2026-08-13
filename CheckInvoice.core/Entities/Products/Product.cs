namespace CheckInvoice.core.Entities.Products;

public class Product
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public long? SubDepartmentId { get; set; }
    public long? MediaTypeId { get; set; }
    public string? UnitOfMeasure { get; set; }
    public decimal? Price { get; set; }
    public decimal? MinStock { get; set; }
    public decimal? MaxStock { get; set; }
    public bool IsActive { get; set; } = true;
}