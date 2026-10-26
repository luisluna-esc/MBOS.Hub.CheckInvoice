using CheckInvoice.core.Entities.Movements;
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

    private static string? PendingStatusLabel(string? status) => status switch
    {
        PendingReceiptStatuses.Pending => "Pendiente de factura",
        PendingReceiptStatuses.Validated => "Validada con factura",
        PendingReceiptStatuses.Voided => "Anulada",
        _ => null
    };
}
