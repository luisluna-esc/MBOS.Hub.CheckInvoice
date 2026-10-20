using System.Globalization;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Las Entradas (compras y devoluciones) ya no eligen el período de almacén: se escribe la fecha
// de ingreso y el período es el de ese mes. La fecha solo puede ser del mes anterior o del actual,
// y nunca posterior a hoy (hora de Bolivia).
internal static class MovementDates
{
    // La tabla no tiene una restricción única sobre el nombre del período, así que dos pedidos al
    // mismo tiempo podían crear el mismo mes varias veces. Todo lo que crea o renombra un período
    // pasa por este candado. Es estático porque los servicios son Scoped y el backend corre en una
    // sola instancia.
    public static readonly SemaphoreSlim PeriodWriteLock = new(1, 1);

    public static DateTime MinEntryDate
    {
        get
        {
            var today = BoliviaTime.Today;
            return new DateTime(today.Year, today.Month, 1).AddMonths(-1);
        }
    }

    public static bool IsWithinEntryWindow(DateTime date) =>
        date.Date >= MinEntryDate && date.Date <= BoliviaTime.Today;

    public static string EntryWindowMessage(string label) =>
        $"La {label} debe estar entre el {MinEntryDate:dd/MM/yyyy} y hoy ({BoliviaTime.Today:dd/MM/yyyy}).";

    public static string PeriodName(DateTime date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    // Devuelve el período del mes de la fecha y lo crea si todavía no existe.
    public static async Task<WarehousePeriod> GetOrCreatePeriodAsync(IUnitOfWork unitOfWork, DateTime date)
    {
        var name = PeriodName(date);
        var repository = unitOfWork.Repository<WarehousePeriod>();

        await PeriodWriteLock.WaitAsync();
        try
        {
            var period = await repository.Query().FirstOrDefaultAsync(w => w.Name == name);
            if (period is not null)
            {
                return period;
            }

            period = new WarehousePeriod { Name = name };
            await repository.AddAsync(period);
            await unitOfWork.SaveChangesAsync();
            return period;
        }
        finally
        {
            PeriodWriteLock.Release();
        }
    }
}
