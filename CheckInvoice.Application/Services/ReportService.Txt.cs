using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// TXT de los reportes: el mismo contenido que el Excel (mismas consultas, columnas y totales), en
// texto separado por tabulaciones, que se abre en Excel o en el Bloc de notas. UTF-8 con BOM y
// saltos de línea de Windows. Mientras _txtSink no es null, AddSheet escribe aquí en vez de armar
// la hoja del libro.
public partial class ReportService
{
    private StringBuilder? _txtSink;

    // N° de salida ("S 15-10-26", o el N° interno en salidas viejas) para los reportes que listan
    // salidas. Se carga una vez por reporte en los métodos que traen los datos.
    private Dictionary<long, string>? _issueNumbers;

    private async Task LoadIssueNumbersAsync()
    {
        if (_issueNumbers is not null)
        {
            return;
        }
        var issues = await _unitOfWork.Repository<core.Entities.Movements.Issue>().Query()
            .Select(i => new { i.IssueId, i.MonthlyNumber, i.IssueDate })
            .ToListAsync();
        _issueNumbers = issues.ToDictionary(i => i.IssueId, i => IssueNumbering.Format(i.MonthlyNumber, i.IssueDate, i.IssueId));
    }

    private string IssueNo(long issueId) =>
        _issueNumbers?.GetValueOrDefault(issueId) ?? issueId.ToString("00000", CultureInfo.InvariantCulture);

    private string IssueNo(long? issueId) => issueId.HasValue ? IssueNo(issueId.Value) : "—";

    /// <summary>Genera el TXT de un reporte a partir de su versión Excel.</summary>
    public async Task<byte[]> ToTxt(Func<Task<byte[]>> excelReport)
    {
        _txtSink = new StringBuilder();
        try
        {
            await excelReport();
            return Utf8WithBom(_txtSink.ToString());
        }
        finally
        {
            _txtSink = null;
        }
    }

    public async Task<byte[]> GenerateIssuesAccountingTxt(core.QueryFilters.Reports.AccountingReportQueryFilter filter) =>
        BuildAccountingTxt(await GetIssuesAccountingReport(filter));

    public async Task<byte[]> GenerateReceiptsAccountingTxt(core.QueryFilters.Reports.AccountingReportQueryFilter filter) =>
        BuildAccountingTxt(await GetReceiptsAccountingReport(filter));

    /// <summary>Formato de importación contable: las 13 columnas de "Transaction_Entry", separadas por tabulación.</summary>
    private static byte[] BuildAccountingTxt(List<Dtos.Reports.AccountingEntryRowDto> rows)
    {
        var text = new StringBuilder();
        text.AppendJoin('\t', AccountingHeaders).Append("\r\n");
        foreach (var row in rows)
        {
            text.AppendJoin('\t',
                row.AccountCode, row.SubAccountCode, row.FundCode, row.FunctionCode, row.RestrictionCode,
                row.EntityValue.ToString("0.00", AccountingCulture), row.SendMemo, TxtClean(row.Description),
                string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
            text.Append("\r\n");
        }
        return Utf8WithBom(text.ToString());
    }

    private void AddTxtSection<T>(string title, IReadOnlyList<string> info, IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows)
    {
        var text = _txtSink!;
        if (text.Length > 0)
        {
            text.Append("\r\n");
        }
        text.Append($"{InstitutionName} · {InstitutionDetail}\r\n");
        text.Append(ReportTitleCase(title)).Append("\r\n");
        foreach (var line in info)
        {
            text.Append(TxtClean(line)).Append("\r\n");
        }
        text.Append("Expresado en bolivianos (Bs.)\r\n");
        text.Append($"Generado el {BoliviaTime.Now:dd/MM/yyyy HH:mm} por {_excelUsername}\r\n\r\n");

        text.AppendJoin('\t', columns.Select(c => TxtClean(c.Header))).Append("\r\n");
        foreach (var row in rows)
        {
            text.AppendJoin('\t', columns.Select(c => TxtValue(c.Value(row), c.Format))).Append("\r\n");
        }

        if (columns.Any(c => c.Total || c.FixedTotal.HasValue))
        {
            var labelColumn = columns.TakeWhile(c => !c.Total && !c.FixedTotal.HasValue).Count() - 1;
            var totals = columns.Select((column, index) =>
            {
                if (column.FixedTotal is { } fixedTotal)
                {
                    return fixedTotal.ToString("0.00", AccountingCulture);
                }
                if (column.Total)
                {
                    var sum = rows.Sum(r => ToDecimal(column.Value(r)));
                    return sum.ToString("0.00", AccountingCulture);
                }
                return index == labelColumn ? "Total" : string.Empty;
            });
            text.AppendJoin('\t', totals).Append("\r\n");
        }
    }

    /// <summary>Números con coma decimal y sin separador de miles; fechas dd/MM/yyyy.</summary>
    private static string TxtValue(object? value, string? format) => value switch
    {
        null => string.Empty,
        string text => TxtClean(text),
        DateTime date => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
        long number when format == IdFormat => number.ToString("00000", CultureInfo.InvariantCulture),
        int number when format == IdFormat => number.ToString("00000", CultureInfo.InvariantCulture),
        decimal number when format == QuantityFormat => number.ToString("0.##", AccountingCulture),
        decimal number => number.ToString("0.00", AccountingCulture),
        double number => number.ToString("0.00", AccountingCulture),
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => TxtClean(value.ToString())
    };

    private static decimal ToDecimal(object? value) => value switch
    {
        decimal number => number,
        double number => (decimal)number,
        long number => number,
        int number => number,
        _ => 0m
    };

    private static string TxtClean(string? text) =>
        (text ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();

    private static byte[] Utf8WithBom(string text) =>
        Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();
}
