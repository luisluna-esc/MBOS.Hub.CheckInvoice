using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

// Estilo común de todos los PDF. Antes cada reporte armaba su propio encabezado y usaba una
// cuadrícula completa con bordes negros y totales en gris oscuro (aspecto "clásico" que cansa
// la vista en listas largas). Ahora: un solo azul (el mismo del sistema, brand-500), texto
// gris oscuro en lugar de negro puro, solo líneas horizontales finas y fondos muy suaves para
// encabezados y totales. Cambiar aquí cambia todos los reportes.
public partial class ReportService
{
    private const string ReportAccent = "#1D3456";      // azul del sistema
    private const string ReportInk = "#1F2937";         // texto normal
    private const string ReportMuted = "#64748B";       // etiquetas y datos secundarios
    private const string ReportLine = "#E2E8F0";        // líneas entre filas
    private const string ReportLineStrong = "#B8C2D3";  // línea bajo encabezados y totales
    private const string ReportHeaderFill = "#EEF2F7";  // fondo de encabezados de tabla
    private const string ReportSoftFill = "#F6F8FB";    // subtotales y franja de filtros
    private const string ReportTotalFill = "#E3E9F2";   // totales generales

    private const string InstitutionName = "IGLESIA ADVENTISTA DEL SÉPTIMO DÍA";
    private const string InstitutionDetail = "Misión Boliviana Occidental del Sur · La Paz, Bolivia";

    // Encabezado de los reportes: logo e institución a la izquierda, título a la derecha y una
    // franja suave con los filtros ("Almacén: X", "Periodo: Y"...). Cada línea se separa en
    // etiqueta y valor en el primer ": " para mostrarlas como pares, no como frases centradas.
    private static void ReportHeader(IContainer container, string title, params string?[] info) =>
        ReportHeaderCore(container, title, true, info);

    // Igual que ReportHeader, para hojas sin montos (ej. la hoja de firmas de un lote): sin la
    // línea "Expresado en bolivianos".
    private static void ReportHeaderWithoutCurrency(IContainer container, string title, params string?[] info) =>
        ReportHeaderCore(container, title, false, info);

    private static void ReportHeaderCore(IContainer container, string title, bool inBolivianos, string?[] info)
    {
        container.PaddingBottom(8).Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(96).Height(35).AlignMiddle().Image(GetLogoBytes()).FitArea();
                row.ConstantItem(12);
                row.ConstantItem(0.8f).PaddingVertical(4).Background(ReportLineStrong);
                row.ConstantItem(10);
                row.RelativeItem().AlignMiddle().Column(c =>
                {
                    c.Item().Text(InstitutionName).SemiBold().FontSize(8.5f).FontColor(ReportAccent);
                    c.Item().Text(InstitutionDetail).FontSize(7.5f).FontColor(ReportMuted);
                });
                row.RelativeItem(1.3f).AlignRight().AlignMiddle().Column(c =>
                {
                    c.Item().AlignRight().Text(title).Bold().FontSize(15).FontColor(ReportAccent);
                    if (inBolivianos)
                    {
                        c.Item().AlignRight().Text("Expresado en bolivianos (Bs.)").FontSize(7.5f).FontColor(ReportMuted);
                    }
                });
            });

            var pairs = info.Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
            if (pairs.Count > 0)
            {
                column.Item().PaddingTop(10).Background(ReportSoftFill).BorderLeft(2.5f).BorderColor(ReportAccent)
                    .PaddingVertical(5).PaddingHorizontal(10).Inlined(inlined =>
                    {
                        inlined.HorizontalSpacing(22);
                        inlined.VerticalSpacing(4);
                        foreach (var line in pairs)
                        {
                            var split = line!.IndexOf(": ", StringComparison.Ordinal);
                            var label = split > 0 ? line[..split] : "";
                            var value = split > 0 ? line[(split + 2)..] : line;
                            inlined.Item().Column(c =>
                            {
                                if (label.Length > 0)
                                {
                                    c.Item().Text(label.ToUpperInvariant()).FontSize(6.5f).SemiBold().LetterSpacing(0.04f).FontColor(ReportMuted);
                                }
                                c.Item().Text(value).FontSize(8.5f).FontColor(ReportInk);
                            });
                        }
                    });
            }
        });
    }

    private static readonly HashSet<string> TitleSmallWords = ["de", "del", "a", "al", "la", "el", "en", "y", "con", "por"];

    // "SALIDA DE ALMACÉN · DEVOLUCIÓN" pasa a "Salida de Almacén · Devolución": los títulos en
    // mayúsculas sostenidas se leen peor y se ven anticuados.
    private static string ReportTitleCase(string text)
    {
        if (text != text.ToUpperInvariant())
        {
            return text;
        }
        var words = text.ToLowerInvariant().Split(' ');
        for (var i = 0; i < words.Length; i++)
        {
            if (words[i].Length > 0 && (i == 0 || !TitleSmallWords.Contains(words[i])))
            {
                words[i] = char.ToUpperInvariant(words[i][0]) + words[i][1..];
            }
        }
        return string.Join(' ', words);
    }

    private static void ReportFooter(IContainer container, string username)
    {
        container.PaddingTop(6).Column(column =>
        {
            column.Item().LineHorizontal(0.5f).LineColor(ReportLine);
            column.Item().PaddingTop(4).DefaultTextStyle(x => x.FontSize(7).FontColor(ReportMuted)).Row(row =>
            {
                row.RelativeItem().Text($"InvenTrack · Generado por {username}");
                row.RelativeItem().AlignCenter().Text($"{BoliviaTime.Now:dd/MM/yyyy HH:mm}");
                row.RelativeItem().AlignRight().Text(x =>
                {
                    x.Span("Página ");
                    x.CurrentPageNumber();
                    x.Span(" de ");
                    x.TotalPages();
                });
            });
        });
    }

    // Título de sección dentro del contenido (ej. "Cuentas por Cobrar", "Pagos").
    private static void SectionTitle(IContainer container, string title) =>
        container.PaddingBottom(4).Text(title).Bold().FontSize(10.5f).FontColor(ReportAccent);

    private static IContainer HeaderCell(IContainer container) =>
        container.Background(ReportHeaderFill).BorderBottom(1).BorderColor(ReportLineStrong)
            .PaddingVertical(4).PaddingHorizontal(3).MinHeight(22).AlignMiddle()
            .DefaultTextStyle(x => x.SemiBold().FontColor(ReportAccent));

    private static IContainer BodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(ReportLine).PaddingVertical(3.5f).PaddingHorizontal(3);

    private static IContainer SubtotalCell(IContainer container) =>
        container.Background(ReportSoftFill).BorderBottom(0.5f).BorderColor(ReportLineStrong)
            .PaddingVertical(3.5f).PaddingHorizontal(3).DefaultTextStyle(x => x.FontColor(ReportAccent));

    private static IContainer CategoryTotalCell(IContainer container) =>
        container.Background(ReportTotalFill).BorderBottom(1).BorderColor(ReportAccent)
            .PaddingVertical(4).PaddingHorizontal(3).DefaultTextStyle(x => x.FontColor(ReportAccent));

    // Fila de grupo (categoría, producto, transferencia) que abre un bloque de filas.
    private static IContainer GroupTitleCell(IContainer container) =>
        container.PaddingTop(12).PaddingBottom(3).PaddingHorizontal(2)
            .DefaultTextStyle(x => x.FontColor(ReportAccent));

    private static IContainer SubGroupTitleCell(IContainer container) =>
        container.PaddingTop(3).PaddingBottom(3).PaddingHorizontal(2)
            .DefaultTextStyle(x => x.SemiBold().FontColor(ReportMuted));

    // Comprobantes impresos (salida, transferencia, cuenta por cobrar): mismo lenguaje visual que
    // los reportes, con un poco más de aire porque son pocas filas.
    private static IContainer VoucherHeaderCell(IContainer container) =>
        container.Background(ReportHeaderFill).BorderBottom(1).BorderColor(ReportLineStrong)
            .Padding(4).MinHeight(26).AlignMiddle()
            .DefaultTextStyle(x => x.SemiBold().FontSize(7).FontColor(ReportAccent));

    private static IContainer VoucherBodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(ReportLine).Padding(4).DefaultTextStyle(x => x.FontSize(8));

    private static IContainer VoucherTotalCell(IContainer container) =>
        container.Background(ReportTotalFill).BorderBottom(1).BorderColor(ReportAccent).Padding(4)
            .DefaultTextStyle(x => x.FontSize(8).FontColor(ReportAccent));

    private static void SignatureBox(ColumnDescriptor column, string? name)
    {
        column.Item().Height(28);
        column.Item().BorderTop(0.8f).BorderColor(ReportLineStrong).PaddingTop(3).AlignCenter()
            .Text(name ?? "").FontSize(8).FontColor(ReportInk);
    }

    // Encabezado compacto de las notas en media carta (nota de ingreso, salida, transferencia):
    // logo e institución, título, QR y el número del documento en un recuadro suave; debajo, los
    // datos del documento en una franja, cada uno en una sola línea.
    private static void HalfLetterHeader(
        IContainer container, string title, string numberLabel, string numberText, string? numberSubText, byte[]? qr,
        IEnumerable<(string Label, string Value, float Weight)[]> infoRows, string? note)
    {
        container.Column(column =>
        {
            column.Item().Row(row =>
            {
                row.ConstantItem(72).Height(27).AlignMiddle().Image(GetLogoBytes()).FitArea();
                row.ConstantItem(8);
                row.ConstantItem(0.8f).PaddingVertical(3).Background(ReportLineStrong);
                row.ConstantItem(8);
                row.RelativeItem().AlignMiddle().Column(c =>
                {
                    c.Item().Text(InstitutionName).SemiBold().FontSize(7).FontColor(ReportAccent);
                    c.Item().Text(InstitutionDetail).FontSize(6.5f).FontColor(ReportMuted);
                    c.Item().PaddingTop(3).Text(ReportTitleCase(title)).Bold().FontSize(10).FontColor(ReportAccent);
                });
                if (qr is not null)
                {
                    row.ConstantItem(42).Height(42).Image(qr).FitArea();
                    row.ConstantItem(6);
                }
                row.ConstantItem(100).Background(ReportHeaderFill).CornerRadius(4).PaddingVertical(4).PaddingHorizontal(3).AlignMiddle().Column(c =>
                {
                    c.Item().AlignCenter().Text(numberLabel).FontSize(6).SemiBold().FontColor(ReportMuted);
                    c.Item().AlignCenter().Text(numberText).Bold().FontSize(12.5f).FontColor(ReportAccent);
                    if (numberSubText is not null)
                    {
                        c.Item().AlignCenter().Text(numberSubText).FontSize(6).FontColor(ReportMuted);
                    }
                });
            });

            column.Item().PaddingTop(6).Background(ReportSoftFill).BorderLeft(2.5f).BorderColor(ReportAccent)
                .PaddingVertical(4).PaddingHorizontal(8).Column(band =>
                {
                    band.Spacing(2);
                    foreach (var infoRow in infoRows)
                    {
                        band.Item().Row(row =>
                        {
                            row.Spacing(12);
                            foreach (var (label, value, weight) in infoRow)
                            {
                                row.RelativeItem(weight).Text(t =>
                                {
                                    t.ClampLines(1);
                                    t.Span(label.ToUpperInvariant() + "  ").FontSize(6).SemiBold().FontColor(ReportMuted);
                                    t.Span(value);
                                });
                            }
                        });
                    }
                    if (!string.IsNullOrWhiteSpace(note))
                    {
                        band.Item().Text(t =>
                        {
                            t.ClampLines(1);
                            t.Span("OBSERVACIÓN  ").FontSize(6).SemiBold().FontColor(ReportMuted);
                            t.Span(note);
                        });
                    }
                });
        });
    }

    private static void NoteFooter(IContainer container, string printedBy, int pageNumber, int pageCount)
    {
        container.PaddingTop(3).DefaultTextStyle(x => x.FontSize(6.5f).FontColor(ReportMuted)).Row(row =>
        {
            row.RelativeItem().Text($"InvenTrack · Impreso por {printedBy} · {BoliviaTime.Now:dd/MM/yyyy HH:mm}");
            row.RelativeItem().AlignRight().Text($"Página {pageNumber} de {pageCount}");
        });
    }

    private static IContainer NoteHeaderCell(IContainer container) =>
        container.Background(ReportHeaderFill).BorderBottom(1).BorderColor(ReportLineStrong).Padding(2).MinHeight(20).AlignMiddle()
            .DefaultTextStyle(x => x.SemiBold().FontSize(6.5f).FontColor(ReportAccent));

    private static IContainer NoteBodyCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(ReportLine).PaddingVertical(2).PaddingHorizontal(3).DefaultTextStyle(x => x.FontSize(7.5f));

    private static IContainer NoteCarryCell(IContainer container) =>
        container.Background(ReportSoftFill).BorderBottom(0.5f).BorderColor(ReportLineStrong).PaddingVertical(2).PaddingHorizontal(3)
            .DefaultTextStyle(x => x.FontSize(7.5f).FontColor(ReportMuted));

    private static IContainer NoteTotalCell(IContainer container) =>
        container.Background(ReportTotalFill).BorderBottom(1).BorderColor(ReportAccent).PaddingVertical(2.5f).PaddingHorizontal(3)
            .DefaultTextStyle(x => x.FontSize(7.5f).FontColor(ReportAccent));

    // Recuadro de una firma (digitado, verificado, aprobado): fondo suave y esquinas redondeadas.
    // Alto fijo: firmado, digitado o vacío (para firmar a mano), los tres miden lo mismo.
    private const float SignatureCardHeight = 50;

    private static IContainer SignatureCard(IContainer container) =>
        container.Height(SignatureCardHeight).Background(ReportSoftFill).Border(0.6f).BorderColor(ReportLine).CornerRadius(4)
            .Padding(4).AlignMiddle();

    private static void SignatureCaption(ColumnDescriptor column, string title, float fontSize) =>
        column.Item().PaddingTop(2).AlignCenter().Text(title).FontSize(fontSize - 0.5f).SemiBold().LetterSpacing(0.04f).FontColor(ReportMuted);

    private static void SummaryTextCard(IContainer container, string label, string value)
    {
        container
            .Background(ReportSoftFill)
            .BorderLeft(2.5f)
            .BorderColor(ReportAccent)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text(label.ToUpperInvariant()).FontSize(7).SemiBold().FontColor(ReportMuted);
                column.Item().PaddingTop(2).Text(value).FontSize(14).Bold().FontColor(ReportAccent);
            });
    }
}
