namespace CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = [];
    public int TotalRecords { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
}