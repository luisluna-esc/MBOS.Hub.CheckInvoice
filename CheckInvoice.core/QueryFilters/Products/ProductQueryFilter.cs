namespace CheckInvoice.core.QueryFilters.Products;

public class ProductQueryFilter
{
    public long? ProductId { get; set; }
    public string? Code { get; set; }
    public long? DepartmentId { get; set; }
    public long? SubDepartmentId { get; set; }
    public bool? IsActive { get; set; }
    public long? WarehouseId { get; set; }
    /// <summary>Solo productos con stock (en el almacén pedido o, sin almacén, en alguno).</summary>
    public bool? InStock { get; set; }
}