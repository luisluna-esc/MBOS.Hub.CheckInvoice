namespace CheckInvoice.Application.Dtos.Catalogs;

public class MediaTypeDto
{
    public long MediaTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}