namespace CheckInvoice.Application.Services;

// La app guarda los momentos exactos (CreatedAt, PaymentDate, TransferDate...) en UTC, pero el
// negocio opera en Bolivia (UTC-4, sin horario de verano). Todo lo que el servidor imprime o
// decide por "hoy" (PDFs, fecha de emisión por defecto, períodos) pasa por aquí para no correr
// fechas 4 horas: un pago a las 21:00 en Bolivia ya es el día siguiente en UTC.
//
// Las fechas de calendario (IssueDate de Entradas/Salidas, DueDate) NO se convierten: ya son
// un día de Bolivia guardado a las 00:00, y convertirlas las correría al día anterior.
internal static class BoliviaTime
{
    private static readonly TimeZoneInfo Zone = ResolveZone();

    public static DateTime Now => FromUtc(DateTime.UtcNow);

    public static DateTime Today => Now.Date;

    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    private static TimeZoneInfo ResolveZone()
    {
        // "America/La_Paz" existe en Linux (Docker) y en Windows con ICU; el ID de Windows queda
        // de respaldo, y si ninguno está, una zona fija UTC-4 (Bolivia no cambia de horario).
        foreach (var id in new[] { "America/La_Paz", "SA Western Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("Bolivia", TimeSpan.FromHours(-4), "Bolivia", "Bolivia");
    }
}
