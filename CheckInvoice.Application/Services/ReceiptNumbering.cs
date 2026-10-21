using System.Globalization;
using System.Text.RegularExpressions;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Número de la nota de ingreso: correlativo dentro del mes de la fecha de ingreso, con prefijo y
// año como los lotes contables: "I 15-01-26" es la nota 15 de enero de 2026. Las entradas
// registradas tarde toman el siguiente número de SU mes.
internal static partial class ReceiptNumbering
{
    // Se toma antes de calcular el número y se suelta después de guardar la entrada, para que dos
    // registros al mismo tiempo no saquen el mismo número.
    public static readonly SemaphoreSlim Lock = new(1, 1);

    public static async Task<int> NextAsync(IUnitOfWork unitOfWork, DateTime entryDate)
    {
        var monthStart = new DateTime(entryDate.Year, entryDate.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var last = await unitOfWork.Repository<Receipt>().Query()
            .Where(r => r.IssueDate >= monthStart && r.IssueDate < nextMonth)
            .MaxAsync(r => r.MonthlyNumber);
        return (last ?? 0) + 1;
    }

    public const string Prefix = "I";
    // Prefijo que se usó antes: se sigue aceptando al buscar por número.
    private const string LegacyPrefix = "MB";

    public static string Format(int? monthlyNumber, DateTime entryDate, long receiptId) =>
        monthlyNumber.HasValue
            ? $"{Prefix} {monthlyNumber.Value}-{entryDate.ToString("MM-yy", CultureInfo.InvariantCulture)}"
            : receiptId.ToString(CultureInfo.InvariantCulture);

    // Acepta "I 15-01-26" (o "MB 15-01-26"), "15-01-26", "15-01-2026" o "15-01". Sin año, el más reciente.
    public static bool TryParse(string? text, out int number, out int month, out int? year)
    {
        number = 0;
        month = 0;
        year = null;
        var clean = (text ?? string.Empty).Trim();
        if (clean.StartsWith(LegacyPrefix, StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[LegacyPrefix.Length..].Trim();
        }
        else if (clean.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[Prefix.Length..].Trim();
        }
        var match = NotePattern().Match(clean);
        if (!match.Success)
        {
            return false;
        }
        number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        month = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        if (match.Groups[3].Success)
        {
            year = int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
            if (year < 100)
            {
                year += 2000;
            }
        }
        return month is >= 1 and <= 12;
    }

    [GeneratedRegex(@"^(\d{1,5})-(\d{1,2})(?:[-/](\d{2}|\d{4}))?$")]
    private static partial Regex NotePattern();
}
