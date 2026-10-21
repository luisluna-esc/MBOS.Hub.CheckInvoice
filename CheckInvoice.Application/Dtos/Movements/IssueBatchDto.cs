namespace CheckInvoice.Application.Dtos.Movements;

/// <summary>Salida por lote: los mismos datos para todos los pastores y la cantidad de cada uno.</summary>
public class IssueBatchRequestDto
{
    public long? IssueTypeId { get; set; }
    public long WarehouseId { get; set; }
    public DateTime? IssueDate { get; set; }
    public long? PrintTypeId { get; set; }
    public string? Description { get; set; }
    public bool SendToAccountsReceivable { get; set; }
    public string? PaymentType { get; set; }
    public string? PaymentDetail { get; set; }
    public DateOnly? DueDate { get; set; }
    public List<IssueBatchPastorRequestDto> Pastors { get; set; } = [];
}

public class IssueBatchPastorRequestDto
{
    public long ClientId { get; set; }
    public List<IssueDetailRequestDto> Details { get; set; } = [];
}
