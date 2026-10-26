using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.QueryFilters.Movements;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

// Ingresos pendientes de factura: lo que llegó sin factura, cuánto se entregó y cuánto queda en
// el almacén. Es aparte de los demás reportes porque estas notas todavía no cuentan en ellos.
public partial class ReportService
{
    public async Task<byte[]> GeneratePendingReceiptsPdf(PendingReceiptQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var notes = await PendingReceiptService.Filtered(_unitOfWork, filter)
            .OrderBy(n => n.ReceivedDate).ThenBy(n => n.PendingReceiptId)
            .ToListAsync();
        var rows = await PendingReceiptService.LoadAsync(_unitOfWork, notes);

        var info = new[]
        {
            $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}",
            $"Estado: {PendingStatusLabel(filter.Status) ?? "Todas"}",
            $"Notas: {rows.Count}"
        };

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        const string title = "Ingresos Pendientes de Factura";
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(8));

                page.Header().Element(c => ReportHeaderWithoutCurrency(c, title, info));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(55);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(50);
                        columns.ConstantColumn(55);
                        columns.ConstantColumn(55);
                        columns.ConstantColumn(55);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Material");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Recibido");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Entregado");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Queda");
                    });

                    foreach (var note in rows)
                    {
                        table.Cell().ColumnSpan(6).Element(GroupTitleCell).Text(t =>
                        {
                            t.Span(note.NoteNumber).Bold().FontSize(9);
                            t.Span($"  ·  Recibido el {note.ReceivedDate:dd/MM/yyyy}  ·  {note.WarehouseName ?? "—"}").FontSize(8);
                            if (!string.IsNullOrWhiteSpace(note.SupplierName))
                            {
                                t.Span($"  ·  {note.SupplierName}").FontSize(8);
                            }
                            t.Span($"  ·  {PendingStatusLabel(note.Status)}").FontSize(8);
                            if (note.ReceiptNoteNumber is not null)
                            {
                                var invoice = note.InvoiceNumber is null ? "" : $", factura {note.InvoiceNumber}";
                                t.Span($" (entrada {note.ReceiptNoteNumber}{invoice})").FontSize(8);
                            }
                        });
                        if (!string.IsNullOrWhiteSpace(note.Description))
                        {
                            table.Cell().ColumnSpan(6).Element(BodyCell).Text(note.Description).Italic().FontColor(ReportMuted);
                        }

                        foreach (var line in note.Lines)
                        {
                            table.Cell().Element(BodyCell).Text(line.Code ?? "");
                            table.Cell().Element(BodyCell).Text(line.Name);
                            table.Cell().Element(BodyCell).AlignCenter().Text(line.UnitMeasure ?? "");
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(line.Received));
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(line.Delivered));
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(line.Remaining)).Bold();
                        }

                        // Las entregas de la nota, una por línea.
                        foreach (var delivery in note.Deliveries.Where(d => !d.IsVoided))
                        {
                            var who = delivery.ClientName ?? delivery.IssueTypeName ?? "Entrega";
                            var what = string.Join(", ", delivery.Lines.Select(l => $"{Qty(l.Quantity)} {l.Name}"));
                            var issue = delivery.IssueNumber is null ? "" : $" (salida {delivery.IssueNumber})";
                            table.Cell().ColumnSpan(6).Element(SubtotalCell)
                                .Text($"Entregado el {delivery.DeliveryDate:dd/MM/yyyy} a {who}{issue}: {what}").FontSize(7.5f);
                        }
                    }

                    if (rows.Count == 0)
                    {
                        table.Cell().ColumnSpan(6).Element(BodyCell).AlignCenter().Text("No hay notas con estos filtros.");
                    }
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, title).GeneratePdf();
    }

    // Formulario de Recepción de una nota sin factura, como la planilla que ya usaban: lo recibido
    // de cada artículo y, cuando se une la factura, su precio, total y N° de factura.
    public async Task<byte[]?> GeneratePendingReceiptNotePdf(long pendingReceiptId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var entity = await _unitOfWork.Repository<PendingReceipt>().GetByIdAsync(pendingReceiptId);
        if (entity is null)
        {
            return null;
        }
        var note = (await PendingReceiptService.LoadAsync(_unitOfWork, [entity]))[0];

        var productIds = note.Lines.Select(l => l.ProductId).ToList();
        var departments = await (
            from p in _unitOfWork.Repository<Product>().Query()
            join d in _unitOfWork.Repository<Department>().Query() on p.DepartmentId equals d.DepartmentId
            where productIds.Contains(p.ProductId)
            select d.Name).Distinct().ToListAsync();

        // Ya validada: precios de la entrada que se creó con la factura.
        var prices = new Dictionary<long, decimal>();
        if (note.ReceiptId.HasValue)
        {
            prices = await _unitOfWork.Repository<ReceiptDetail>().Query()
                .Where(d => d.ReceiptId == note.ReceiptId.Value)
                .GroupBy(d => d.ProductId)
                .ToDictionaryAsync(g => g.Key, g => g.First().UnitPrice ?? g.First().UnitCost);
        }
        var hasPrices = prices.Count > 0;
        var invoice = note.InvoiceNumber ?? "";

        var printedBy = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            printedBy = user?.Username ?? printedBy;
        }

        // Solo los datos que tienen valor: si no hay proveedor, no sale ni el título.
        var fields = new List<(string Label, string? Value)>
        {
            ("Proveedor", note.SupplierName),
            ("Fecha de recepción", note.ReceivedDate.ToString("dd/MM/yyyy")),
            ("Departamento", departments.Count > 0 ? string.Join(", ", departments) : null),
            ("Almacén", note.WarehouseName),
            ("Estado", PendingStatusLabel(note.Status)),
            ("Entrada", note.ReceiptNoteNumber)
        }.Where(f => !string.IsNullOrWhiteSpace(f.Value)).ToList();
        var infoRows = fields.Chunk(2)
            .Select(pair => pair.Select((f, i) => (f.Label, f.Value!, i == 0 ? 2f : 1f)).ToArray())
            .ToList();
        // Precio y total solo cuando ya hay factura; la columna de factura solo si tiene número.
        var hasInvoice = !string.IsNullOrWhiteSpace(invoice);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(NotePage);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(8));

                page.Header().Element(c => HalfLetterHeader(c, "Formulario de Recepción", "RECEPCIÓN N°", note.NoteNumber, null, null, infoRows, note.Description));

                page.Content().PaddingTop(8).Column(column =>
                {
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.RelativeColumn(1);
                            columns.ConstantColumn(62);
                            if (hasPrices)
                            {
                                columns.ConstantColumn(62);
                                columns.ConstantColumn(66);
                            }
                            if (hasInvoice)
                            {
                                columns.ConstantColumn(80);
                            }
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(NoteHeaderCell).AlignCenter().Text("N°");
                            header.Cell().Element(NoteHeaderCell).Text("Artículos");
                            header.Cell().Element(NoteHeaderCell).AlignRight().Text("Cantidad recibida");
                            if (hasPrices)
                            {
                                header.Cell().Element(NoteHeaderCell).AlignRight().Text("Precio unitario");
                                header.Cell().Element(NoteHeaderCell).AlignRight().Text("Total");
                            }
                            if (hasInvoice)
                            {
                                header.Cell().Element(NoteHeaderCell).AlignCenter().Text("Nota de entrega o factura");
                            }
                        });

                        var row = 0;
                        foreach (var line in note.Lines)
                        {
                            row++;
                            var price = prices.TryGetValue(line.ProductId, out var p) ? p : (decimal?)null;
                            table.Cell().Element(NoteBodyCell).AlignCenter().Text(row.ToString());
                            table.Cell().Element(NoteBodyCell).Text(line.Name);
                            table.Cell().Element(NoteBodyCell).AlignRight().Text(Qty(line.Received));
                            if (hasPrices)
                            {
                                table.Cell().Element(NoteBodyCell).AlignRight().Text(price?.ToString("N2", ReportCulture) ?? "");
                                table.Cell().Element(NoteBodyCell).AlignRight().Text(price is null ? "" : (price.Value * line.Received).ToString("N2", ReportCulture));
                            }
                            if (hasInvoice)
                            {
                                table.Cell().Element(NoteBodyCell).AlignCenter().Text(invoice);
                            }
                        }

                        table.Cell().Element(NoteTotalCell).Text("");
                        table.Cell().Element(NoteTotalCell).Text("Total").Bold();
                        table.Cell().Element(NoteTotalCell).AlignRight().Text(Qty(note.Lines.Sum(l => l.Received))).Bold();
                        if (hasPrices)
                        {
                            table.Cell().Element(NoteTotalCell).Text("");
                            table.Cell().Element(NoteTotalCell).AlignRight()
                                .Text(note.Lines.Sum(l => prices.GetValueOrDefault(l.ProductId) * l.Received).ToString("N2", ReportCulture)).Bold();
                        }
                        if (hasInvoice)
                        {
                            table.Cell().Element(NoteTotalCell).Text("");
                        }
                    });


                    column.Item().PaddingTop(40).Row(signatures =>
                    {
                        signatures.Spacing(40);
                        signatures.RelativeItem().Column(c => SignatureBox(c, SignatureLabel("Entregado por", note.SupplierName)));
                        signatures.RelativeItem().Column(c => SignatureBox(c, SignatureLabel("Recibido por", note.CreatedByName)));
                    });
                });

                page.Footer().Element(c => ReportFooter(c, printedBy));
            });
        });

        return WithTitle(document, $"Formulario de Recepción {note.NoteNumber}").GeneratePdf();
    }

    private static string SignatureLabel(string title, string? name) =>
        string.IsNullOrWhiteSpace(name) ? title : $"{title}: {name}";

    private static string? PendingStatusLabel(string? status) => status switch
    {
        PendingReceiptStatuses.Pending => "Pendiente de factura",
        PendingReceiptStatuses.Validated => "Validada con factura",
        PendingReceiptStatuses.Voided => "Anulada",
        _ => null
    };
}
