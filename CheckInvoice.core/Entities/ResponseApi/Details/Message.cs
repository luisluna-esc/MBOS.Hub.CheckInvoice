namespace CheckInvoice.core.Entities.ResponseApi.Details;

public class Message
{
    public MessageType Type { get; set; }
    public string Description { get; set; } = string.Empty;
}