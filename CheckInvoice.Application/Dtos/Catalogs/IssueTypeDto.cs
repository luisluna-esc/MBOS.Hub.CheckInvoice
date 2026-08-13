namespace CheckInvoice.Application.Dtos.Catalogs;

public class IssueTypeDto
{
    public long IssueTypeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}