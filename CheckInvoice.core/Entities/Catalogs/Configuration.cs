namespace CheckInvoice.core.Entities.Catalogs;

public class Configuration
{
    public long ConfigurationId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Description { get; set; }
}