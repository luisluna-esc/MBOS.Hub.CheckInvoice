using System.Text.Json;

namespace CheckInvoice.Application.Dtos.Governance;

public class ChangeRequestCreateDto
{
    public string TableName { get; set; } = string.Empty;
    public long RecordId { get; set; }
    public string Action { get; set; } = string.Empty;
    public Dictionary<string, JsonElement>? ProposedData { get; set; }
    public string Reason { get; set; } = string.Empty;
}
