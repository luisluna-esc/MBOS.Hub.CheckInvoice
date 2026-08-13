namespace CheckInvoice.core.Entities.Catalogs;

public class MediaType
{
    public long MediaTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}