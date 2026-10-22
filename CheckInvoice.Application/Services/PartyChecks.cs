using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Reglas para no duplicar personas ni pastores. Las usan los validadores al guardar y los
// endpoints que el formulario consulta mientras se escribe (para avisar antes de "Guardar").
public static class PartyChecks
{
    /// <summary>Nombre de quien ya tiene ese NIT/C.I. (sin contar la identidad que se edita), o null si está libre.</summary>
    public static async Task<string?> TaxIdOwnerAsync(IUnitOfWork unitOfWork, string? taxId, long excludePartyId)
    {
        var value = taxId?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }
        return await unitOfWork.Repository<Party>().Query()
            .Where(p => p.PartyId != excludePartyId && p.TaxId == value)
            .Select(p => p.Name)
            .FirstOrDefaultAsync();
    }

    /// <summary>Pastor que ya está en ese distrito (sin contar al cliente que se edita), o null.</summary>
    public static Task<string?> DistrictPastorAsync(IUnitOfWork unitOfWork, long? districtId, long excludeClientId) =>
        PastorWhereAsync(unitOfWork, excludeClientId, c => c.DistrictId == districtId, districtId.HasValue);

    /// <summary>Pastor que ya está en esa iglesia (sin contar al cliente que se edita), o null.</summary>
    public static Task<string?> ChurchPastorAsync(IUnitOfWork unitOfWork, long? churchId, long excludeClientId) =>
        PastorWhereAsync(unitOfWork, excludeClientId, c => c.ChurchId == churchId, churchId.HasValue);

    private static async Task<string?> PastorWhereAsync(
        IUnitOfWork unitOfWork, long excludeClientId, System.Linq.Expressions.Expression<Func<Client, bool>> filter, bool hasValue)
    {
        if (!hasValue)
        {
            return null;
        }
        return await (
            from c in unitOfWork.Repository<Client>().Query().Where(filter)
            join p in unitOfWork.Repository<Party>().Query() on c.PartyId equals p.PartyId
            where c.IsPastor && c.IsActive && c.ClientId != excludeClientId
            select p.Name).FirstOrDefaultAsync();
    }
}
