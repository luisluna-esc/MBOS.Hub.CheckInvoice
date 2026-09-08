namespace CheckInvoice.core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IGenericRepository<TEntity> Repository<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync();

    /// <summary>
    /// Ejecuta SQL crudo (p.ej. SELECT * FROM sp_algo(...)) y mapea el resultado a un
    /// tipo no registrado en el modelo de EF, por nombre de columna. Usar "{0}", "{1}", ...
    /// como marcadores posicionales; se parametrizan igual que con FromSqlRaw, sin riesgo
    /// de inyección SQL.
    /// </summary>
    Task<List<T>> SqlQueryAsync<T>(string sql, params object[] parameters) where T : class;
}