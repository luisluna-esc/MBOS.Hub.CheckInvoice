namespace CheckInvoice.core.Entities.Catalogs;

public class Mission
{
    public long MissionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}