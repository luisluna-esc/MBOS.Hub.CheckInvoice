namespace CheckInvoice.Application.Dtos.Catalogs;

public class MissionDto
{
    public long MissionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}