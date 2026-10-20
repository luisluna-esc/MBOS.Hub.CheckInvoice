using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

// Comprobantes en carta vertical (Salida en hoja y Transferencia): mismo formato que la nota de
// ingreso. Cada fila ocupa una línea, así que se sabe de antemano cuántas hojas salen; las
// hojas intermedias cierran con "Pasa a la siguiente hoja" y la siguiente abre con "Viene de
// la hoja anterior". El total y las firmas van en la última hoja.
public partial class ReportService
{
    private sealed record VoucherColumn(string Header, float? Width, bool AlignRight = false, bool AlignCenter = false);

    private sealed record VoucherRow(string[] Cells, decimal Amount);

    private sealed record HalfLetterVoucher(
        string Title,
        string NumberLabel,
        string NumberText,
        string? NumberSubText,
        List<(string Label, string Value)[]> InfoRows,
        string? Note,
        List<VoucherColumn> Columns,
        List<VoucherRow> Rows,
        string TotalLabel,
        // Filas que entran en la última hoja (depende del alto de las firmas).
        int RowsOnLastPage,
        Action<ColumnDescriptor> Signatures,
        string PrintedBy,
        byte[]? Qr);

    private static List<List<VoucherRow>> SplitVoucherRows(List<VoucherRow> rows, int rowsOnLastPage)
    {
        var pages = new List<List<VoucherRow>>();
        var index = 0;
        while (rows.Count - index > rowsOnLastPage)
        {
            var take = Math.Min(NoteRowsPerPage, rows.Count - index - 1);
            pages.Add(rows.GetRange(index, take));
            index += take;
        }
        pages.Add(rows.GetRange(index, rows.Count - index));
        return pages;
    }

    private static Document BuildHalfLetterVoucher(HalfLetterVoucher voucher)
    {
        var pages = SplitVoucherRows(voucher.Rows, voucher.RowsOnLastPage);
        var total = voucher.Rows.Sum(r => r.Amount);
        var columnCount = voucher.Columns.Count;

        return Document.Create(container =>
        {
            decimal carried = 0m;
            for (var pageIndex = 0; pageIndex < pages.Count; pageIndex++)
            {
                var pageRows = pages[pageIndex];
                var isFirst = pageIndex == 0;
                var isLast = pageIndex == pages.Count - 1;
                var pageNumber = pageIndex + 1;
                var carriedIn = carried;
                var carriedOut = carriedIn + pageRows.Sum(r => r.Amount);
                carried = carriedOut;

                container.Page(page =>
                {
                    page.Size(NotePage);
                    page.Margin(28);
                    page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(7.5f));

                    page.Header().Element(c => HalfLetterHeader(c, voucher.Title, voucher.NumberLabel, voucher.NumberText, voucher.NumberSubText, voucher.Qr,
                        voucher.InfoRows.Select(r => r.Select(p => (p.Label, p.Value, 1f)).ToArray()), voucher.Note));

                    page.Content().PaddingTop(6).ScaleToFit().Column(mainColumn =>
                    {
                        mainColumn.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                foreach (var column in voucher.Columns)
                                {
                                    if (column.Width.HasValue)
                                    {
                                        columns.ConstantColumn(column.Width.Value);
                                    }
                                    else
                                    {
                                        columns.RelativeColumn(1);
                                    }
                                }
                            });

                            table.Header(header =>
                            {
                                foreach (var column in voucher.Columns)
                                {
                                    header.Cell().Element(NoteHeaderCell).AlignCenter().Text(column.Header);
                                }
                            });

                            if (!isFirst)
                            {
                                table.Cell().ColumnSpan((uint)(columnCount - 1)).Element(NoteCarryCell).AlignRight().Text("VIENE DE LA HOJA ANTERIOR").Italic();
                                table.Cell().Element(NoteCarryCell).AlignRight().Text(carriedIn.ToString("N2", ReportCulture));
                            }

                            foreach (var row in pageRows)
                            {
                                for (var i = 0; i < columnCount; i++)
                                {
                                    var column = voucher.Columns[i];
                                    var cell = table.Cell().Element(NoteBodyCell);
                                    if (column.AlignRight)
                                    {
                                        cell = cell.AlignRight();
                                    }
                                    else if (column.AlignCenter)
                                    {
                                        cell = cell.AlignCenter();
                                    }
                                    cell.Text(row.Cells[i]).ClampLines(1);
                                }
                            }

                            var closingLabel = isLast ? voucher.TotalLabel : "SUBTOTAL · PASA A LA SIGUIENTE HOJA";
                            var closingAmount = isLast ? total : carriedOut;
                            table.Cell().ColumnSpan((uint)(columnCount - 1)).Element(NoteTotalCell).AlignRight().Text(closingLabel).Bold();
                            table.Cell().Element(NoteTotalCell).AlignRight().Text(closingAmount.ToString("N2", ReportCulture)).Bold();
                        });

                        if (isLast)
                        {
                            mainColumn.Item().PaddingTop(10).Column(voucher.Signatures);
                        }
                    });

                    page.Footer().Element(c => NoteFooter(c, voucher.PrintedBy, pageNumber, pages.Count));
                });
            }
        });
    }

    // Firmas de entrega en papel (quien entrega y quien recibe) y, debajo, las firmas digitales.
    private static void DeliveryAndDigitalSignatures(
        ColumnDescriptor column, string leftName, string leftTitle, string? rightName, string rightTitle, DocumentSignatures signatures)
    {
        column.Item().PaddingTop(16).Row(row =>
        {
            row.Spacing(20);
            row.RelativeItem().Column(c =>
            {
                c.Item().BorderTop(0.8f).BorderColor(ReportLineStrong).PaddingTop(2).AlignCenter().Text(leftName).FontSize(7);
                c.Item().AlignCenter().Text(leftTitle).FontSize(6).SemiBold().FontColor(ReportMuted);
            });
            row.RelativeItem().Column(c =>
            {
                c.Item().BorderTop(0.8f).BorderColor(ReportLineStrong).PaddingTop(2).AlignCenter().Text(rightName ?? " ").FontSize(7);
                c.Item().AlignCenter().Text(rightTitle).FontSize(6).SemiBold().FontColor(ReportMuted);
            });
        });

        column.Item().PaddingTop(6).Row(row =>
        {
            row.Spacing(20);
            row.RelativeItem().Column(c => DigitalSignatureBox(c, "VERIFICADO POR", signatures.Verified));
            row.RelativeItem().Column(c => DigitalSignatureBox(c, "APROBADO POR", signatures.Approved));
        });
        column.Item().PaddingTop(2).Text($"Versión V.{signatures.Version} del documento · Firma digital en reemplazo de la firma en papel.")
            .FontSize(6).FontColor(ReportMuted);
    }
}
