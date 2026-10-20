namespace CheckInvoice.Application.Dtos.Movements;

// Solicitud de corrección de una Salida: la salida completa como debería quedar, con el motivo.
// El costo de cada producto no se escribe: sale del costo promedio del almacén, como al registrarla.
public class IssueEditRequestDto
{
    public long IssueId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long? IssueTypeId { get; set; }
    public long? ClientId { get; set; }
    public string? Description { get; set; }
    // Fecha de la salida: solo se puede mover dentro del mismo mes (su N° es de ese mes).
    public DateTime? IssueDate { get; set; }
    public List<IssueDetailRequestDto> Details { get; set; } = [];
}

// Para listar y revisar: qué cambia (antes y después). Reusa las filas de cambios de Entradas.
public class IssueEditDto
{
    public long ChangeRequestId { get; set; }
    public long IssueId { get; set; }
    public string NoteNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string? RequestedByName { get; set; }
    public DateTime RequestedAt { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewNotes { get; set; }
    public List<ReceiptEditFieldChangeDto> HeaderChanges { get; set; } = [];
    public List<ReceiptEditLineChangeDto> Lines { get; set; } = [];
}
