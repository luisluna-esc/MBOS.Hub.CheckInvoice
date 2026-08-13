namespace CheckInvoice.core.Entities.Catalogs;

public class IssueType
{
    public long IssueTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}