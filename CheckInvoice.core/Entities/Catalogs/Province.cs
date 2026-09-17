namespace CheckInvoice.core.Entities.Catalogs;

public class Province
{
    public long ProvinceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}