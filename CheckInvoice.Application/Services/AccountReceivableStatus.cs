namespace CheckInvoice.Application.Services;

// "late" (Pago retrasado) no se guarda en account_receivable.status: se deriva al leer, así
// cambia solo al día siguiente de la fecha límite, sin tareas programadas ni pasos manuales.
// Mismo criterio que sp_get_account_receivables (storedProcedures.sql), con la fecha de Bolivia.
internal static class AccountReceivableStatus
{
    public static bool IsLate(string status, DateOnly? dueDate) =>
        status == "pending" && dueDate.HasValue && dueDate.Value < DateOnly.FromDateTime(BoliviaTime.Today);

    public static string Effective(string status, DateOnly? dueDate) =>
        IsLate(status, dueDate) ? "late" : status;
}
