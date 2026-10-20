namespace CheckInvoice.Application.Dtos.Products;

public class ProductDto
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public long? DepartmentId { get; set; }
    public long? SubDepartmentId { get; set; }
    public long? MediaTypeId { get; set; }
    public bool IsActive { get; set; }
    // Solo de lectura: si tiene foto y una versión para que el navegador no muestre una foto vieja.
    public bool HasImage { get; set; }
    public long? ImageVersion { get; set; }
}