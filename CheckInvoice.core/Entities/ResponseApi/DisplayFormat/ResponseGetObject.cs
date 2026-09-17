using System.Net;
using System.Text.Json.Serialization;
using CheckInvoice.core.Entities.ResponseApi.Details;

namespace CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

public class ResponseGetObject
{
    public object Data { get; set; } = new();
    public Message[] Messages { get; set; } = [];
    [JsonIgnore]
    public HttpStatusCode StatusCode { get; set; }
}