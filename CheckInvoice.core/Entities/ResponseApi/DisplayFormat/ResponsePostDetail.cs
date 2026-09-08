namespace CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

public class ResponsePostDetail
{
    public string Process { get; set; } = string.Empty;
    public int AffectedRows { get; set; }
    public int IdGenerated { get; set; }
}