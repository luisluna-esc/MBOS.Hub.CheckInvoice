namespace CheckInvoice.core.QueryFilters.Parties;

public class SupplierQueryFilter
{
    public long? SupplierId { get; set; }
    public string? Code { get; set; }
    public string? TaxId { get; set; }
    public long? CountryId { get; set; }
    public bool? IsActive { get; set; }
}