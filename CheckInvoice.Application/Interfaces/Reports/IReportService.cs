using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.core.QueryFilters.Reports;

namespace CheckInvoice.Application.Interfaces.Reports;

public interface IReportService
{
    Task<byte[]> GenerateFinancialDashboardPdf();
    Task<FinancialDashboardDataDto> GetFinancialDashboardData();
    Task<List<StockReportRowDto>> GetStockReport(StockReportQueryFilter filter);
    Task<byte[]> GenerateStockReportPdf(StockReportQueryFilter filter);
    Task<byte[]> GenerateInventoryCountTemplatePdf(StockReportQueryFilter filter);
    Task<List<StockReportRowDto>> GetStockByDepartmentReport(StockByDepartmentReportQueryFilter filter);
    Task<byte[]> GenerateStockByDepartmentReportPdf(StockByDepartmentReportQueryFilter filter);
    Task<List<KardexReportRowDto>> GetKardexReport(KardexReportQueryFilter filter);
    Task<byte[]> GenerateKardexReportPdf(KardexReportQueryFilter filter);
    Task<List<InventoryCountReportRowDto>> GetInventoryCountReport(InventoryCountReportQueryFilter filter);
    Task<byte[]> GenerateInventoryCountReportPdf(InventoryCountReportQueryFilter filter);
    Task<List<IssuesReportRowDto>> GetIssuesReport(IssuesReportQueryFilter filter);
    Task<byte[]> GenerateIssuesReportPdf(IssuesReportQueryFilter filter);
    Task<byte[]?> GenerateIssueVoucherPdf(long issueId, long? printTypeId = null);
    Task<byte[]?> GenerateTransferVoucherPdf(long transferId);
    Task<List<ReceiptsReportRowDto>> GetReceiptsReport(ReceiptsReportQueryFilter filter);
    Task<byte[]> GenerateReceiptsReportPdf(ReceiptsReportQueryFilter filter);
    Task<byte[]?> GenerateReceiptVoucherPdf(long receiptId);
    Task<List<KardexByProductReportRowDto>> GetKardexByProductReport(KardexByProductReportQueryFilter filter);
    Task<byte[]> GenerateKardexByProductReportPdf(KardexByProductReportQueryFilter filter);
    Task<PastorFieldReportDataDto> GetPastorFieldReportData(PastorFieldReportQueryFilter filter);
    Task<byte[]> GeneratePastorFieldReportPdf(PastorFieldReportQueryFilter filter);
    Task<List<AccountReceivablesReportRowDto>> GetAccountReceivablesReport(AccountReceivablesReportQueryFilter filter);
    Task<byte[]> GenerateAccountReceivablesReportPdf(AccountReceivablesReportQueryFilter filter);
}
