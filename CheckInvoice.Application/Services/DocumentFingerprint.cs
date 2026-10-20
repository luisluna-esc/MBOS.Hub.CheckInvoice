using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CheckInvoice.core.Entities.Governance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Huella del contenido de un documento al momento de firmarlo: almacén, fechas, proveedor o
// cliente, y cada línea (producto, cantidad, costo). Se guarda con la firma; si después el
// documento cambia, la huella actual ya no coincide y la firma se muestra como no válida.
// La anulación no entra en la huella: anular tiene su propio circuito (solicitud y aprobación
// del Contador) y no debe invalidar firmas hechas antes.
internal static class DocumentFingerprint
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static async Task<string?> ComputeAsync(IUnitOfWork unitOfWork, string documentType, long documentId)
    {
        var builder = new StringBuilder();
        switch (documentType)
        {
            case DocumentReviews.Receipt:
            {
                var receipt = await unitOfWork.Repository<Receipt>().GetByIdAsync(documentId);
                if (receipt is null)
                {
                    return null;
                }
                builder.Append(Invariant, $"R|{receipt.WarehouseId}|{receipt.SupplierId}|{receipt.ReceiptTypeId}|{receipt.IssueDate:yyyyMMdd}|");
                builder.Append(Invariant, $"{receipt.PurchaseDate:yyyyMMdd}|{receipt.InvoiceNumber}|{receipt.TaxId}|{receipt.InvoiceTotal:0.00}");
                var lines = await unitOfWork.Repository<ReceiptDetail>().Query()
                    .Where(d => d.ReceiptId == documentId)
                    .OrderBy(d => d.ReceiptDetailId)
                    .Select(d => new { d.ProductId, d.Quantity, d.UnitCost, d.TotalCost })
                    .ToListAsync();
                foreach (var line in lines)
                {
                    builder.Append(Invariant, $"|{line.ProductId}:{line.Quantity:0.00}:{line.UnitCost:0.00}:{line.TotalCost:0.00}");
                }
                break;
            }
            case DocumentReviews.Issue:
            {
                var issue = await unitOfWork.Repository<Issue>().GetByIdAsync(documentId);
                if (issue is null)
                {
                    return null;
                }
                builder.Append(Invariant, $"I|{issue.WarehouseId}|{issue.ClientId}|{issue.IssueTypeId}|{issue.IssueDate:yyyyMMdd}");
                var lines = await unitOfWork.Repository<IssueDetail>().Query()
                    .Where(d => d.IssueId == documentId)
                    .OrderBy(d => d.IssueDetailId)
                    .Select(d => new { d.ProductId, d.Quantity, d.UnitCost, d.TotalCost })
                    .ToListAsync();
                foreach (var line in lines)
                {
                    builder.Append(Invariant, $"|{line.ProductId}:{line.Quantity:0.00}:{line.UnitCost:0.00}:{line.TotalCost:0.00}");
                }
                break;
            }
            case DocumentReviews.Transfer:
            {
                var transfer = await unitOfWork.Repository<Transfer>().GetByIdAsync(documentId);
                if (transfer is null)
                {
                    return null;
                }
                builder.Append(Invariant, $"T|{transfer.SourceWarehouseId}|{transfer.DestinationWarehouseId}|{transfer.TransferDate:yyyyMMddHHmmss}");
                var lines = await unitOfWork.Repository<TransferDetail>().Query()
                    .Where(d => d.TransferId == documentId)
                    .OrderBy(d => d.TransferDetailId)
                    .Select(d => new { d.ProductId, d.Quantity, d.UnitPrice })
                    .ToListAsync();
                foreach (var line in lines)
                {
                    builder.Append(Invariant, $"|{line.ProductId}:{line.Quantity:0.00}:{line.UnitPrice:0.00}");
                }
                break;
            }
            default:
                return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    // Código corto que se imprime junto a la firma (ej. 3F9A-07C2-B1D4). Une la huella del
    // contenido con quién firmó, qué y cuándo.
    public static string SignatureCode(DocumentReview review)
    {
        var source = $"{review.ContentHash}|{review.DocumentType}|{review.DocumentId}|{review.Version}|{review.Action}|{review.UserId}|{review.At.ToString("yyyyMMddHHmmss", Invariant)}";
        var hex = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
        return $"{hex[..4]}-{hex[4..8]}-{hex[8..12]}";
    }
}
