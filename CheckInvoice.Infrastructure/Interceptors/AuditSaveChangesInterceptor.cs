using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CheckInvoice.Infrastructure.Interceptors;

public class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUserService;
    private List<PendingAuditEntry> _pendingEntries = [];

    public AuditSaveChangesInterceptor(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            _pendingEntries = eventData.Context.ChangeTracker.Entries()
                .Where(e => e.Entity is not AuditLog && e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => new PendingAuditEntry(e, ResolveAction(e.State), e.Metadata.GetTableName() ?? e.Metadata.ClrType.Name))
                .ToList();
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null && _pendingEntries.Count > 0)
        {
            var appUserId = _currentUserService.AppUserId;

            var logs = _pendingEntries.Select(pending => new AuditLog
            {
                AppUserId = appUserId,
                TableName = pending.TableName,
                RecordId = GetRecordId(pending.Entry),
                LogDate = DateTime.UtcNow,
                Action = pending.Action
            }).ToList();

            _pendingEntries = [];

            eventData.Context.Set<AuditLog>().AddRange(logs);
            await eventData.Context.SaveChangesAsync(cancellationToken);
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private static string ResolveAction(EntityState state) => state switch
    {
        EntityState.Added => "INSERT",
        EntityState.Modified => "UPDATE",
        EntityState.Deleted => "DELETE",
        _ => string.Empty
    };

    private static long? GetRecordId(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count != 1)
            return null;

        var value = entry.Property(primaryKey.Properties[0].Name).CurrentValue;
        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            _ => null
        };
    }

    private record PendingAuditEntry(EntityEntry Entry, string Action, string TableName);
}