namespace CheckInvoice.Application.Dtos.Finance;

public class InstallmentDto
{
    public long InstallmentId { get; set; }
    public long AccountReceivableId { get; set; }
    public int InstallmentNumber { get; set; }
    public decimal InstallmentAmount { get; set; }
    public DateOnly DueDate { get; set; }
    public string Status { get; set; } = string.Empty;
}