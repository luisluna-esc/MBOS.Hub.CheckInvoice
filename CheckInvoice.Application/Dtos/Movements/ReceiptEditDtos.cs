namespace CheckInvoice.Application.Dtos.Movements;

// Solicitud de corrección de una Entrada: la nota completa como debería quedar, con el motivo.
public class ReceiptEditRequestDto
{
    public long ReceiptId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public long? SupplierId { get; set; }
    public string? TaxId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Description { get; set; }
    public decimal? InvoiceTotal { get; set; }
    // Fecha de ingreso: solo se puede mover dentro del mismo mes (su N° de nota es de ese mes).
    public DateTime? IssueDate { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public List<ReceiptDetailRequestDto> Details { get; set; } = [];
}

// Lo que se guarda en change_request.proposed_data: la corrección y cuánto baja cada producto
// (esas unidades quedan apartadas mientras el Contador decide, como en las anulaciones).
public class ReceiptEditProposal
{
    public long WarehouseId { get; set; }
    public ReceiptEditRequestDto Edit { get; set; } = new();
    public List<ReceiptEditDecrease> Decreases { get; set; } = [];
}

public class ReceiptEditDecrease
{
    public long ProductId { get; set; }
    public decimal Quantity { get; set; }
}

// Para listar y revisar: qué cambia (antes y después).
public class ReceiptEditDto
{
    public long ChangeRequestId { get; set; }
    public long ReceiptId { get; set; }
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

public class ReceiptEditFieldChangeDto
{
    public string Field { get; set; } = string.Empty;
    public string? Before { get; set; }
    public string? After { get; set; }
}

public class ReceiptEditLineChangeDto
{
    public long ProductId { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal QuantityBefore { get; set; }
    public decimal QuantityAfter { get; set; }
    public decimal PriceBefore { get; set; }
    public decimal PriceAfter { get; set; }
}

public class ReceiptEditReviewDto
{
    public string? ReviewNotes { get; set; }
}
