namespace CheckInvoice.core.QueryFilters.Catalogs;

public class WarehouseQueryFilter
{
    public long? WarehouseId { get; set; }
    public bool? IsActive { get; set; }
    /// <summary>Solo almacenes con productos en stock (para Salidas: el almacén sale cuando tiene productos).</summary>
    public bool? WithStock { get; set; }
}