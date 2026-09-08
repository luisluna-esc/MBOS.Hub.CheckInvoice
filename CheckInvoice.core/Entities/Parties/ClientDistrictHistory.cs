namespace CheckInvoice.core.Entities.Parties;

public class ClientDistrictHistory
{
    public long ClientDistrictHistoryId { get; set; }
    public long ClientId { get; set; }
    public long DistrictId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
}