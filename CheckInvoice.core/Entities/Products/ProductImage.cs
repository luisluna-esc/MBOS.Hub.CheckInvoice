namespace CheckInvoice.core.Entities.Products;

// Una foto por producto, guardada en la base (no en disco): viaja con los respaldos de la base y
// no necesita otro servidor. El frontend la achica antes de subirla; el límite es 5 MB.
public class ProductImage
{
    public long ProductId { get; set; }
    public byte[] Content { get; set; } = [];
    public string ContentType { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
