namespace CheckInvoice.Application.Dtos.Catalogs;

public class ProvinceDto
{
    public long ProvinceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}