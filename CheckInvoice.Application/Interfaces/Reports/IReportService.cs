using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.core.QueryFilters.Reports;

namespace CheckInvoice.Application.Interfaces.Reports;

public interface IReportService
{
    Task<FinancialDashboardDataDto> GetFinancialDashboardData();
    Task<List<StockReportRowDto>> GetStockReport(StockReportQueryFilter filter);
    Task<byte[]> GenerateStockReportPdf(StockReportQueryFilter filter);
    Task<byte[]> GenerateStockReportExcel(StockReportQueryFilter filter);
    Task<byte[]> GenerateInventoryCountTemplatePdf(StockReportQueryFilter filter);
    Task<List<StockReportRowDto>> GetStockByDepartmentReport(StockByDepartmentReportQueryFilter filter);
    Task<byte[]> GenerateStockByDepartmentReportPdf(StockByDepartmentReportQueryFilter filter);
    Task<byte[]> GenerateStockByDepartmentReportExcel(StockByDepartmentReportQueryFilter filter);
    Task<List<KardexReportRowDto>> GetKardexReport(KardexReportQueryFilter filter);
    Task<byte[]> GenerateKardexReportPdf(KardexReportQueryFilter filter);
    Task<byte[]> GenerateKardexReportExcel(KardexReportQueryFilter filter);
    Task<List<InventoryCountReportRowDto>> GetInventoryCountReport(InventoryCountReportQueryFilter filter);
    Task<byte[]> GenerateInventoryCountReportPdf(InventoryCountReportQueryFilter filter);
    Task<byte[]> GenerateInventoryCountReportExcel(InventoryCountReportQueryFilter filter);
    Task<List<IssuesReportRowDto>> GetIssuesReport(IssuesReportQueryFilter filter);
    Task<byte[]> GenerateIssuesReportPdf(IssuesReportQueryFilter filter);
    Task<byte[]> GenerateIssuesReportExcel(IssuesReportQueryFilter filter);
    Task<byte[]?> GenerateIssueVoucherPdf(long issueId, long? printTypeId = null, string? publicBaseUrl = null);
    Task<byte[]?> GenerateTransferVoucherPdf(long transferId);
    Task<List<ReceiptsReportRowDto>> GetReceiptsReport(ReceiptsReportQueryFilter filter);
    Task<byte[]> GenerateReceiptsReportPdf(ReceiptsReportQueryFilter filter);
    Task<byte[]> GenerateReceiptsReportExcel(ReceiptsReportQueryFilter filter);
    Task<List<TransfersReportRowDto>> GetTransfersReport(TransfersReportQueryFilter filter);
    Task<byte[]> GenerateTransfersReportPdf(TransfersReportQueryFilter filter);
    Task<byte[]> GenerateTransfersReportExcel(TransfersReportQueryFilter filter);
    Task<List<AccountingEntryRowDto>> GetIssuesAccountingReport(AccountingReportQueryFilter filter);
    Task<byte[]> GenerateIssuesAccountingExcel(AccountingReportQueryFilter filter);
    Task<byte[]?> GenerateIssueBatchPdf(long issueBatchId);
    Task<List<AccountingEntryRowDto>> GetReceiptsAccountingReport(AccountingReportQueryFilter filter);
    Task<byte[]> GenerateReceiptsAccountingExcel(AccountingReportQueryFilter filter);
    /// <summary>TXT (separado por tabulaciones) con el mismo contenido que la versión Excel del reporte.</summary>
    Task<byte[]> ToTxt(Func<Task<byte[]>> excelReport);
    Task<byte[]> GenerateIssuesAccountingTxt(AccountingReportQueryFilter filter);
    Task<byte[]> GenerateReceiptsAccountingTxt(AccountingReportQueryFilter filter);
    Task<byte[]?> GenerateReceiptVoucherPdf(long receiptId, string? publicBaseUrl = null);
    Task<List<KardexByProductReportRowDto>> GetKardexByProductReport(KardexByProductReportQueryFilter filter);
    Task<byte[]> GenerateKardexByProductReportPdf(KardexByProductReportQueryFilter filter);
    Task<byte[]> GenerateKardexByProductReportExcel(KardexByProductReportQueryFilter filter);
    Task<PastorFieldReportDataDto> GetPastorFieldReportData(PastorFieldReportQueryFilter filter);
    Task<byte[]> GeneratePastorFieldReportPdf(PastorFieldReportQueryFilter filter);
    Task<byte[]> GeneratePastorFieldReportExcel(PastorFieldReportQueryFilter filter);
    Task<List<AccountReceivablesReportRowDto>> GetAccountReceivablesReport(AccountReceivablesReportQueryFilter filter);
    Task<byte[]> GenerateAccountReceivablesReportPdf(AccountReceivablesReportQueryFilter filter);
    Task<byte[]> GenerateAccountReceivablesReportExcel(AccountReceivablesReportQueryFilter filter);
    Task<byte[]?> GenerateAccountReceivableVoucherPdf(long accountReceivableId);
}
