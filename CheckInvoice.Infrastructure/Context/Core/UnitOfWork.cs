using System.Collections.Concurrent;
using CheckInvoice.core.Interfaces;
using CheckInvoice.Infrastructure.Context;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Infrastructure.Context.Core;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ConcurrentDictionary<Type, object> _repositories = new();

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        return (IGenericRepository<TEntity>)_repositories.GetOrAdd(
            typeof(TEntity),
            _ => new GenericRepository<TEntity>(_context));
    }

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

    public Task<List<T>> SqlQueryAsync<T>(string sql, params object[] parameters) where T : class
        => _context.Database.SqlQueryRaw<T>(sql, parameters).ToListAsync();

    public void Dispose()
    {
        _context.Dispose();
        GC.SuppressFinalize(this);
    }
}