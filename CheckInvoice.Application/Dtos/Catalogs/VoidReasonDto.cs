namespace CheckInvoice.Application.Dtos.Catalogs;

public class VoidReasonDto
{
    public long VoidReasonId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
