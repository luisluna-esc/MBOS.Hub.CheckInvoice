namespace CheckInvoice.Application.Dtos.Movements;

// Resultado de revisar (o cargar) el Excel del inventario inicial.
public class InitialInventoryImportDto
{
    public List<InitialInventoryLineDto> Lines { get; set; } = [];
    public List<string> Errors { get; set; } = [];
    public decimal TotalQuantity { get; set; }
    public decimal TotalCost { get; set; }
    // Solo al confirmar: la nota de ingreso creada.
    public long? ReceiptId { get; set; }
    public string? NoteNumber { get; set; }
}

public class InitialInventoryLineDto
{
    public long ProductId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}
