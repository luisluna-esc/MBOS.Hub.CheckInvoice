using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Governance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

// Nota de ingreso (carta vertical).
public partial class ReportService
{
    // Notas (ingreso, salida en hoja y transferencia): hoja carta vertical, 8,5 x 11 pulgadas.
    private static readonly PageSize NotePage = new(612, 792);

    // Filas por hoja. Cada descripción ocupa una sola línea (se recorta con "…"), así que el alto
    // de cada fila es fijo y se sabe de antemano cuántas hojas salen y dónde cortar el subtotal.
    // La última hoja lleva menos filas porque ahí van el total y las firmas.
    private const int NoteRowsPerPage = 42;
    private const int NoteRowsOnLastPage = 32;

    private sealed record NoteLine(string? Code, string Name, string? Unit, decimal Quantity, decimal UnitPrice, decimal UnitCost, decimal TotalCost);

    private sealed record NoteHeader(
        Receipt Receipt,
        string NoteNumber,
        string? ReceiptTypeName,
        string WarehouseName,
        string? SupplierName,
        string? CreatedByName,
        List<NoteLine> Lines);

    private async Task<NoteHeader?> LoadNoteAsync(long receiptId)
    {
        var receipt = await _unitOfWork.Repository<Receipt>().GetByIdAsync(receiptId);
        if (receipt is null)
        {
            return null;
        }

        var receiptTypeName = receipt.ReceiptTypeId.HasValue
            ? (await _unitOfWork.Repository<ReceiptType>().GetByIdAsync(receipt.ReceiptTypeId.Value))?.Name
            : null;
        var warehouseName = receipt.WarehouseId.HasValue
            ? (await _unitOfWork.Repository<Warehouse>().GetByIdAsync(receipt.WarehouseId.Value))?.Name ?? "—"
            : "—";

        string? supplierName = null;
        if (receipt.SupplierId.HasValue)
        {
            supplierName = await (
                from s in _unitOfWork.Repository<Supplier>().Query()
                join p in _unitOfWork.Repository<Party>().Query() on s.PartyId equals p.PartyId
                where s.SupplierId == receipt.SupplierId.Value
                select s.LegalName ?? p.Name).FirstOrDefaultAsync();
        }

        string? createdByName = null;
        if (receipt.CreatedById.HasValue)
        {
            var creator = await _unitOfWork.Repository<AppUser>().GetByIdAsync(receipt.CreatedById.Value);
            createdByName = creator is null ? null : $"{creator.FirstName} {creator.LastName}".Trim();
        }

        var lines = await (
            from d in _unitOfWork.Repository<ReceiptDetail>().Query()
            join p in _unitOfWork.Repository<Product>().Query() on d.ProductId equals p.ProductId
            join m in _unitOfWork.Repository<MediaType>().Query() on p.MediaTypeId equals m.MediaTypeId into media
            from m in media.DefaultIfEmpty()
            where d.ReceiptId == receiptId
            orderby d.ReceiptDetailId
            select new NoteLine(p.Code, p.Name, m != null ? m.Name : null, d.Quantity, d.UnitPrice ?? d.UnitCost, d.UnitCost, d.TotalCost))
            .ToListAsync();

        return new NoteHeader(
            receipt,
            ReceiptNumbering.Format(receipt.MonthlyNumber, receipt.IssueDate, receipt.ReceiptId),
            receiptTypeName,
            warehouseName,
            supplierName,
            createdByName,
            lines);
    }

    // Reparte las líneas en hojas: las intermedias llenas, la última con lugar para total y firmas.
    private static List<List<NoteLine>> SplitIntoPages(List<NoteLine> lines)
    {
        var pages = new List<List<NoteLine>>();
        var index = 0;
        while (lines.Count - index > NoteRowsOnLastPage)
        {
            var take = Math.Min(NoteRowsPerPage, lines.Count - index - 1);
            pages.Add(lines.GetRange(index, take));
            index += take;
        }
        pages.Add(lines.GetRange(index, lines.Count - index));
        return pages;
    }

    private static string ReceiptTypeLabel(string? receiptTypeName) => receiptTypeName switch
    {
        ReceiptCosting.PurchaseWithInvoice => "COMPRA CON FACTURA",
        ReceiptCosting.PurchaseWithRetention => "COMPRA CON RETENCIÓN",
        ReceiptCosting.Donation => "DONACIÓN",
        ReceiptCosting.Adjustment => "AJUSTE DE INVENTARIO",
        ReceiptCosting.InitialInventory => "INVENTARIO INICIAL",
        "Devolucion" => "DEVOLUCIÓN",
        null => "—",
        _ => receiptTypeName.ToUpperInvariant()
    };

    // Firma digital de un paso (verificado o aprobado) de la versión vigente del documento.
    private sealed record DigitalSignature(string Name, DateTime At, string Code, bool Valid);

    private sealed record DocumentSignatures(int Version, DigitalSignature? Verified, DigitalSignature? Approved);

    private async Task<DocumentSignatures> LoadSignaturesAsync(string documentType, long documentId, int version)
    {
        var rows = await (
            from r in _unitOfWork.Repository<DocumentReview>().Query()
            join u in _unitOfWork.Repository<AppUser>().Query() on r.UserId equals u.AppUserId into users
            from u in users.DefaultIfEmpty()
            where r.DocumentType == documentType && r.DocumentId == documentId && r.Version == version
                  && (r.Action == DocumentReviews.Verified || r.Action == DocumentReviews.Approved)
            orderby r.DocumentReviewId
            select new { Review = r, Name = u != null ? u.FirstName + " " + u.LastName : null })
            .ToListAsync();

        var currentHash = await DocumentFingerprint.ComputeAsync(_unitOfWork, documentType, documentId);
        DigitalSignature? Pick(string action)
        {
            var row = rows.LastOrDefault(x => x.Review.Action == action);
            return row is null
                ? null
                : new DigitalSignature(row.Name ?? "—", BoliviaTime.FromUtc(row.Review.At), DocumentFingerprint.SignatureCode(row.Review),
                    row.Review.ContentHash == currentHash);
        }

        return new DocumentSignatures(version, Pick(DocumentReviews.Verified), Pick(DocumentReviews.Approved));
    }

    // Recuadro de firma digital: reemplaza la firma en papel. Sin firma queda "Pendiente".
    private static void DigitalSignatureBox(ColumnDescriptor column, string title, DigitalSignature? signature, float fontSize = 7)
    {
        column.Item().Element(SignatureCard).Column(c =>
        {
            // Sin firma digital el recuadro queda vacío, para firmar a mano (el alto lo fija SignatureCard).
            if (signature is null)
            {
                c.Item().Height(1);
                return;
            }

            c.Item().AlignCenter().Text($"Firmado digitalmente por {signature.Name}").FontSize(fontSize).SemiBold().FontColor(ReportAccent);
            c.Item().AlignCenter().Text($"{signature.At:dd/MM/yyyy HH:mm} · Código {signature.Code}").FontSize(fontSize - 1).FontColor(ReportMuted);
            if (!signature.Valid)
            {
                c.Item().AlignCenter().Text("NO VÁLIDA: el documento cambió después de firmado").FontSize(fontSize - 1).Bold().FontColor(Colors.Red.Darken2);
            }
        });
        SignatureCaption(column, title, fontSize);
    }

    // N° de nota de cada entrada ("I 15-10-26"), para los reportes que listan entradas.
    private async Task<Dictionary<long, string>> ReceiptNoteNumbersAsync(IEnumerable<long> receiptIds)
    {
        var ids = receiptIds.Distinct().ToList();
        var receipts = await _unitOfWork.Repository<Receipt>().Query()
            .Where(r => ids.Contains(r.ReceiptId))
            .Select(r => new { r.ReceiptId, r.MonthlyNumber, r.IssueDate })
            .ToListAsync();
        return receipts.ToDictionary(r => r.ReceiptId, r => ReceiptNumbering.Format(r.MonthlyNumber, r.IssueDate, r.ReceiptId));
    }

    private static byte[] QrPng(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        return new PngByteQRCode(data).GetGraphic(10);
    }

    /// <param name="publicBaseUrl">Dirección del sistema (ej. https://inventrack...). Con ella se imprime
    /// un QR que abre el detalle de la entrada con las fotos; sin ella la nota sale sin QR.</param>
    public async Task<byte[]?> GenerateReceiptVoucherPdf(long receiptId, string? publicBaseUrl = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var note = await LoadNoteAsync(receiptId);
        if (note is null)
        {
            return null;
        }

        var receipt = note.Receipt;
        var isRetention = note.ReceiptTypeName == ReceiptCosting.PurchaseWithRetention;
        var isPurchase = ReceiptCosting.IsPurchaseType(note.ReceiptTypeName);

        var printedBy = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            printedBy = user?.Username ?? printedBy;
        }

        var qr = string.IsNullOrWhiteSpace(publicBaseUrl)
            ? null
            : QrPng($"{publicBaseUrl.TrimEnd('/')}/receipts/view/{receiptId}");

        var signatures = await LoadSignaturesAsync(DocumentReviews.Receipt, receiptId, receipt.ReviewVersion);

        var pages = SplitIntoPages(note.Lines);
        var total = note.Lines.Sum(l => l.TotalCost);
        var registeredAt = BoliviaTime.FromUtc(receipt.CreatedAt);

        var document = Document.Create(container =>
        {
            decimal carried = 0m;
            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var pageLines = pages[pageIndex];
                var isFirst = pageIndex == 0;
                var isLast = pageIndex == pages.Count - 1;
                var pageNumber = pageIndex + 1;
                var carriedIn = carried;
                var carriedOut = carriedIn + pageLines.Sum(l => l.TotalCost);
                carried = carriedOut;

                container.Page(page =>
                {
                    page.Size(NotePage);
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(7.5f));

                    page.Header().Element(c => HalfLetterHeader(c, $"Nota de Ingreso a Almacén · {ReportTitleCase(ReceiptTypeLabel(note.ReceiptTypeName))}",
                        "NOTA DE INGRESO N°", note.NoteNumber, null, qr,
                        [
                            [("Almacén", note.WarehouseName, 3f), ("Fecha de ingreso", receipt.IssueDate.ToString("dd/MM/yyyy"), 2f),
                                ("Fecha de compra", receipt.PurchaseDate?.ToString("dd/MM/yyyy") ?? "—", 2f)],
                            [(note.ReceiptTypeName == ReceiptCosting.Donation ? "Donante" : "Proveedor", note.SupplierName ?? "—", 3f),
                                (isRetention ? "CI" : "NIT", string.IsNullOrWhiteSpace(receipt.TaxId) ? "—" : receipt.TaxId, 2f),
                                (isRetention || !isPurchase ? "N° documento" : "N° factura", string.IsNullOrWhiteSpace(receipt.InvoiceNumber) ? "0" : receipt.InvoiceNumber, 2f)],
                        ],
                        receipt.Description));

                    // Si algo no entrara (una observación muy larga), se achica en vez de crear otra hoja.
                    page.Content().PaddingTop(6).ScaleToFit().Column(mainColumn =>
                    {
                        mainColumn.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(50);
                                columns.RelativeColumn(1);
                                columns.ConstantColumn(54);
                                columns.ConstantColumn(46);
                                if (isRetention)
                                {
                                    columns.ConstantColumn(56);
                                }
                                columns.ConstantColumn(56);
                                columns.ConstantColumn(62);
                            });
                            var columnCount = isRetention ? 7 : 6;

                            table.Header(header =>
                            {
                                header.Cell().Element(NoteHeaderCell).AlignCenter().Text("CÓDIGO");
                                header.Cell().Element(NoteHeaderCell).AlignCenter().Text("DESCRIPCIÓN");
                                header.Cell().Element(NoteHeaderCell).AlignCenter().Text("UNIDAD");
                                header.Cell().Element(NoteHeaderCell).AlignCenter().Text("CANTIDAD");
                                if (isRetention)
                                {
                                    header.Cell().Element(NoteHeaderCell).AlignCenter().Text("PRECIO\nUNIT. (Bs.)");
                                }
                                header.Cell().Element(NoteHeaderCell).AlignCenter().Text(isRetention ? "COSTO UNIT.\nx 0,92 (Bs.)" : "COSTO\nUNIT. (Bs.)");
                                header.Cell().Element(NoteHeaderCell).AlignCenter().Text("COSTO\nTOTAL (Bs.)");
                            });

                            if (!isFirst)
                            {
                                table.Cell().ColumnSpan((uint)(columnCount - 1)).Element(NoteCarryCell).AlignRight().Text("VIENE DE LA HOJA ANTERIOR").Italic();
                                table.Cell().Element(NoteCarryCell).AlignRight().Text(carriedIn.ToString("N2", ReportCulture));
                            }

                            foreach (var line in pageLines)
                            {
                                table.Cell().Element(NoteBodyCell).Text(line.Code ?? "—").ClampLines(1);
                                table.Cell().Element(NoteBodyCell).Text(line.Name).ClampLines(1);
                                table.Cell().Element(NoteBodyCell).AlignCenter().Text(line.Unit ?? "—").ClampLines(1);
                                table.Cell().Element(NoteBodyCell).AlignRight().Text(Qty(line.Quantity));
                                if (isRetention)
                                {
                                    table.Cell().Element(NoteBodyCell).AlignRight().Text(line.UnitPrice.ToString("N2", ReportCulture));
                                }
                                table.Cell().Element(NoteBodyCell).AlignRight().Text(line.UnitCost.ToString("N2", ReportCulture));
                                table.Cell().Element(NoteBodyCell).AlignRight().Text(line.TotalCost.ToString("N2", ReportCulture));
                            }

                            if (isLast)
                            {
                                table.Cell().ColumnSpan((uint)(columnCount - 1)).Element(NoteTotalCell).AlignRight().Text("TOTAL (Bs.)").Bold();
                                table.Cell().Element(NoteTotalCell).AlignRight().Text(total.ToString("N2", ReportCulture)).Bold();
                            }
                            else
                            {
                                table.Cell().ColumnSpan((uint)(columnCount - 1)).Element(NoteTotalCell).AlignRight().Text("SUBTOTAL · PASA A LA SIGUIENTE HOJA").Bold();
                                table.Cell().Element(NoteTotalCell).AlignRight().Text(carriedOut.ToString("N2", ReportCulture)).Bold();
                            }
                        });

                        if (isLast)
                        {
                            if (isPurchase && receipt.InvoiceTotal.HasValue)
                            {
                                mainColumn.Item().PaddingTop(3).AlignRight()
                                    .Text($"Valor total de la compra: {receipt.InvoiceTotal.Value.ToString("N2", ReportCulture)} Bs.").FontSize(7);
                                if (isRetention)
                                {
                                    // Con retención el 100% de la compra es el valor total dividido entre 0,92.
                                    var retentionBase = Math.Round(receipt.InvoiceTotal.Value / ReceiptCosting.RetentionCostFactor, 2, MidpointRounding.AwayFromZero);
                                    mainColumn.Item().AlignRight()
                                        .Text($"100% con retención (÷ 0,92): {retentionBase.ToString("N2", ReportCulture)} Bs.").FontSize(7);
                                }
                            }

                            mainColumn.Item().PaddingTop(10).Row(row =>
                            {
                                row.Spacing(10);
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Element(SignatureCard).Column(box =>
                                    {
                                        box.Item().AlignCenter().Text(note.CreatedByName ?? "—").FontSize(7).Bold();
                                        box.Item().AlignCenter().Text($"{registeredAt:dd/MM/yyyy HH:mm}").FontSize(6).FontColor(ReportMuted);
                                    });
                                    SignatureCaption(c, "DIGITADO POR", 7);
                                });
                                row.RelativeItem().Column(c => DigitalSignatureBox(c, "VERIFICADO POR", signatures.Verified));
                                row.RelativeItem().Column(c => DigitalSignatureBox(c, "APROBADO POR", signatures.Approved));
                            });

                            mainColumn.Item().PaddingTop(3).Text($"Versión V.{signatures.Version} del documento · La firma digital reemplaza a la firma en papel; el código se comprueba en el sistema con el QR.")
                                .FontSize(6).FontColor(ReportMuted);
                        }
                    });

                    page.Footer().Element(c => NoteFooter(c, printedBy, pageNumber, pages.Count));
                });
            }
        });

        return WithTitle(document, $"Nota de Ingreso N° {note.NoteNumber}").GeneratePdf();
    }
}
