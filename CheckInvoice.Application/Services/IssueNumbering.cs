using System.Globalization;
using System.Text.RegularExpressions;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Número de la salida: correlativo dentro del mes de la fecha de la salida, igual que la nota de
// ingreso ("I 15-01-26"): "S 15-01-26" es la salida 15 de enero de 2026. Las salidas registradas
// antes de este cambio no tienen número del mes y siguen mostrando su N° interno ("00019").
internal static partial class IssueNumbering
{
    // Se toma antes de calcular el número y se suelta después de guardar, para que dos registros
    // al mismo tiempo no saquen el mismo número.
    public static readonly SemaphoreSlim Lock = new(1, 1);

    public const string Prefix = "S";

    public static async Task<int> NextAsync(IUnitOfWork unitOfWork, DateTime issueDate)
    {
        var monthStart = new DateTime(issueDate.Year, issueDate.Month, 1);
        var nextMonth = monthStart.AddMonths(1);
        var last = await unitOfWork.Repository<Issue>().Query()
            .Where(i => i.IssueDate >= monthStart && i.IssueDate < nextMonth)
            .MaxAsync(i => i.MonthlyNumber);
        return (last ?? 0) + 1;
    }

    public static string Format(int? monthlyNumber, DateTime issueDate, long issueId) =>
        monthlyNumber.HasValue
            ? $"{Prefix} {monthlyNumber.Value}-{issueDate.ToString("MM-yy", CultureInfo.InvariantCulture)}"
            : issueId.ToString("00000", CultureInfo.InvariantCulture);

    // Acepta "S 15-01-26", "15-01-26", "15-01-2026" o "15-01". Sin año, el más reciente.
    public static bool TryParse(string? text, out int number, out int month, out int? year)
    {
        number = 0;
        month = 0;
        year = null;
        var clean = (text ?? string.Empty).Trim();
        if (clean.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[Prefix.Length..].Trim();
        }
        var match = NumberPattern().Match(clean);
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

    /// <summary>Números de varias salidas, para listados y reportes.</summary>
    public static async Task<Dictionary<long, string>> NumbersAsync(IUnitOfWork unitOfWork, IEnumerable<long> issueIds)
    {
        var ids = issueIds.Distinct().ToList();
        var issues = await unitOfWork.Repository<Issue>().Query()
            .Where(i => ids.Contains(i.IssueId))
            .Select(i => new { i.IssueId, i.MonthlyNumber, i.IssueDate })
            .ToListAsync();
        return issues.ToDictionary(i => i.IssueId, i => Format(i.MonthlyNumber, i.IssueDate, i.IssueId));
    }

    [GeneratedRegex(@"^(\d{1,5})-(\d{1,2})(?:[-/](\d{2}|\d{4}))?$")]
    private static partial Regex NumberPattern();
}
