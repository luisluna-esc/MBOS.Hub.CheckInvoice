namespace CheckInvoice.core.QueryFilters.Pagination;

public class PaginationQueryFilter
{
    public int PageSize { get; set; } = 10;
    public int PageNumber { get; set; } = 1;
    public string? SearchCriteria { get; set; }
}