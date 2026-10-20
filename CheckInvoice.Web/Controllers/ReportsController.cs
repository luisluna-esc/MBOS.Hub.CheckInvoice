using CheckInvoice.Application.Interfaces.Reports;
using CheckInvoice.core.QueryFilters.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CheckInvoice.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly IReportService _reportService;

    public ReportsController(IReportService reportService)
    {
        _reportService = reportService;
    }

    // El modelo de permisos simplificado (visita/trabajo) no distingue por rol, así que el
    // reporte financiero de toda la organización necesita esta restricción explícita además
    // de la política — sin esto, cualquier rol con "visita" (incluido Pastor) podría llamar
    // el endpoint directamente aunque no vea el dashboard en Inicio.
    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("financial-dashboard/data")]
    public async Task<IActionResult> GetFinancialDashboardData()
    {
        var data = await _reportService.GetFinancialDashboardData();
        return Ok(data);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("stock")]
    public async Task<IActionResult> GetStockReport([FromQuery] StockReportQueryFilter filter)
    {
        var rows = await _reportService.GetStockReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("stock/pdf")]
    public async Task<IActionResult> GetStockReportPdf([FromQuery] StockReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateStockReportPdf(filter);
        return File(pdfBytes, "application/pdf", "inventario-general.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("stock/excel")]
    public async Task<IActionResult> GetStockReportExcel([FromQuery] StockReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateStockReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "inventario-general.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("stock/txt")]
    public async Task<IActionResult> GetStockReportTxt([FromQuery] StockReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateStockReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "inventario-general.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("inventory-count-template/pdf")]
    public async Task<IActionResult> GetInventoryCountTemplatePdf([FromQuery] StockReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateInventoryCountTemplatePdf(filter);
        return File(pdfBytes, "application/pdf", "plantilla-conteo-fisico.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("stock-by-department")]
    public async Task<IActionResult> GetStockByDepartmentReport([FromQuery] StockByDepartmentReportQueryFilter filter)
    {
        var rows = await _reportService.GetStockByDepartmentReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("stock-by-department/pdf")]
    public async Task<IActionResult> GetStockByDepartmentReportPdf([FromQuery] StockByDepartmentReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateStockByDepartmentReportPdf(filter);
        return File(pdfBytes, "application/pdf", "inventario-por-departamento.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("stock-by-department/excel")]
    public async Task<IActionResult> GetStockByDepartmentReportExcel([FromQuery] StockByDepartmentReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateStockByDepartmentReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "inventario-por-departamento.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("stock-by-department/txt")]
    public async Task<IActionResult> GetStockByDepartmentReportTxt([FromQuery] StockByDepartmentReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateStockByDepartmentReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "inventario-por-departamento.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("kardex")]
    public async Task<IActionResult> GetKardexReport([FromQuery] KardexReportQueryFilter filter)
    {
        var rows = await _reportService.GetKardexReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("kardex/pdf")]
    public async Task<IActionResult> GetKardexReportPdf([FromQuery] KardexReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateKardexReportPdf(filter);
        return File(pdfBytes, "application/pdf", "kardex-fisico-valorado.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("kardex/excel")]
    public async Task<IActionResult> GetKardexReportExcel([FromQuery] KardexReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateKardexReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "kardex-fisico-valorado.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("kardex/txt")]
    public async Task<IActionResult> GetKardexReportTxt([FromQuery] KardexReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateKardexReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "kardex-fisico-valorado.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("inventory-count")]
    public async Task<IActionResult> GetInventoryCountReport([FromQuery] InventoryCountReportQueryFilter filter)
    {
        var rows = await _reportService.GetInventoryCountReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("inventory-count/pdf")]
    public async Task<IActionResult> GetInventoryCountReportPdf([FromQuery] InventoryCountReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateInventoryCountReportPdf(filter);
        return File(pdfBytes, "application/pdf", "levantamiento-de-inventario.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("inventory-count/excel")]
    public async Task<IActionResult> GetInventoryCountReportExcel([FromQuery] InventoryCountReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateInventoryCountReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "levantamiento-de-inventario.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("inventory-count/txt")]
    public async Task<IActionResult> GetInventoryCountReportTxt([FromQuery] InventoryCountReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateInventoryCountReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "levantamiento-de-inventario.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("issues")]
    public async Task<IActionResult> GetIssuesReport([FromQuery] IssuesReportQueryFilter filter)
    {
        var rows = await _reportService.GetIssuesReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("issues/pdf")]
    public async Task<IActionResult> GetIssuesReportPdf([FromQuery] IssuesReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateIssuesReportPdf(filter);
        return File(pdfBytes, "application/pdf", "salidas-de-almacen.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("issues/excel")]
    public async Task<IActionResult> GetIssuesReportExcel([FromQuery] IssuesReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateIssuesReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "salidas-de-almacen.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("issues/txt")]
    public async Task<IActionResult> GetIssuesReportTxt([FromQuery] IssuesReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateIssuesReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "salidas-de-almacen.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("issue-voucher/{issueId}/pdf")]
    public async Task<IActionResult> GetIssueVoucherPdf(long issueId, [FromQuery] long? printTypeId, [FromServices] IConfiguration configuration)
    {
        var pdfBytes = await _reportService.GenerateIssueVoucherPdf(issueId, printTypeId, PublicBaseUrl(configuration));
        if (pdfBytes is null)
        {
            return NotFound();
        }
        return File(pdfBytes, "application/pdf", $"salida-{issueId}.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("receipts")]
    public async Task<IActionResult> GetReceiptsReport([FromQuery] ReceiptsReportQueryFilter filter)
    {
        var rows = await _reportService.GetReceiptsReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("receipts/pdf")]
    public async Task<IActionResult> GetReceiptsReportPdf([FromQuery] ReceiptsReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateReceiptsReportPdf(filter);
        return File(pdfBytes, "application/pdf", "ingresos-de-almacen.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("receipts/excel")]
    public async Task<IActionResult> GetReceiptsReportExcel([FromQuery] ReceiptsReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateReceiptsReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ingresos-de-almacen.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("receipts/txt")]
    public async Task<IActionResult> GetReceiptsReportTxt([FromQuery] ReceiptsReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateReceiptsReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "ingresos-de-almacen.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("transfers")]
    public async Task<IActionResult> GetTransfersReport([FromQuery] TransfersReportQueryFilter filter)
    {
        var rows = await _reportService.GetTransfersReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("transfers/pdf")]
    public async Task<IActionResult> GetTransfersReportPdf([FromQuery] TransfersReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateTransfersReportPdf(filter);
        return File(pdfBytes, "application/pdf", "transferencias-de-almacen.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("transfers/excel")]
    public async Task<IActionResult> GetTransfersReportExcel([FromQuery] TransfersReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateTransfersReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "transferencias-de-almacen.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("transfers/txt")]
    public async Task<IActionResult> GetTransfersReportTxt([FromQuery] TransfersReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateTransfersReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "transferencias-de-almacen.txt");
    }

    // Salidas para contabilidad: el archivo que se importa en el sistema contable.
    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("issues-accounting")]
    public async Task<IActionResult> GetIssuesAccountingReport([FromQuery] AccountingReportQueryFilter filter)
    {
        var rows = await _reportService.GetIssuesAccountingReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("issues-accounting/excel")]
    public async Task<IActionResult> GetIssuesAccountingExcel([FromQuery] AccountingReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateIssuesAccountingExcel(filter);
        var name = filter.DateFrom.HasValue && filter.DateTo.HasValue
            ? $"salidas-contabilidad-{filter.DateFrom:yyyy-MM-dd}-al-{filter.DateTo:yyyy-MM-dd}.xlsx"
            : "salidas-contabilidad.xlsx";
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("issues-accounting/txt")]
    public async Task<IActionResult> GetIssuesAccountingTxt([FromQuery] AccountingReportQueryFilter filter)
    {
        var txtBytes = await _reportService.GenerateIssuesAccountingTxt(filter);
        var name = filter.DateFrom.HasValue && filter.DateTo.HasValue
            ? $"salidas-contabilidad-{filter.DateFrom:yyyy-MM-dd}-al-{filter.DateTo:yyyy-MM-dd}.txt"
            : "salidas-contabilidad.txt";
        return File(txtBytes, "text/plain; charset=utf-8", name);
    }

    // Ingresos para contabilidad: el mismo archivo, con las entradas en positivo.
    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("receipts-accounting")]
    public async Task<IActionResult> GetReceiptsAccountingReport([FromQuery] AccountingReportQueryFilter filter)
    {
        var rows = await _reportService.GetReceiptsAccountingReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("receipts-accounting/excel")]
    public async Task<IActionResult> GetReceiptsAccountingExcel([FromQuery] AccountingReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateReceiptsAccountingExcel(filter);
        var name = filter.DateFrom.HasValue && filter.DateTo.HasValue
            ? $"ingresos-contabilidad-{filter.DateFrom:yyyy-MM-dd}-al-{filter.DateTo:yyyy-MM-dd}.xlsx"
            : "ingresos-contabilidad.xlsx";
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", name);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("receipts-accounting/txt")]
    public async Task<IActionResult> GetReceiptsAccountingTxt([FromQuery] AccountingReportQueryFilter filter)
    {
        var txtBytes = await _reportService.GenerateReceiptsAccountingTxt(filter);
        var name = filter.DateFrom.HasValue && filter.DateTo.HasValue
            ? $"ingresos-contabilidad-{filter.DateFrom:yyyy-MM-dd}-al-{filter.DateTo:yyyy-MM-dd}.txt"
            : "ingresos-contabilidad.txt";
        return File(txtBytes, "text/plain; charset=utf-8", name);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("receipt-voucher/{receiptId}/pdf")]
    public async Task<IActionResult> GetReceiptVoucherPdf(long receiptId, [FromServices] IConfiguration configuration)
    {
        var pdfBytes = await _reportService.GenerateReceiptVoucherPdf(receiptId, PublicBaseUrl(configuration));
        if (pdfBytes is null)
        {
            return NotFound();
        }
        return File(pdfBytes, "application/pdf", $"ingreso-{receiptId}.pdf");
    }

    // Dirección del sistema para el QR de la nota: la configurada (App:PublicUrl) o, si no hay,
    // la de la página que pidió el PDF.
    private string? PublicBaseUrl(IConfiguration configuration)
    {
        var configured = configuration["App:PublicUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var origin = Request.Headers.Origin.ToString();
        if (!string.IsNullOrWhiteSpace(origin))
        {
            return origin;
        }

        return Uri.TryCreate(Request.Headers.Referer.ToString(), UriKind.Absolute, out var referer)
            ? referer.GetLeftPart(UriPartial.Authority)
            : null;
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("transfer-voucher/{transferId}/pdf")]
    public async Task<IActionResult> GetTransferVoucherPdf(long transferId)
    {
        var pdfBytes = await _reportService.GenerateTransferVoucherPdf(transferId);
        if (pdfBytes is null)
        {
            return NotFound();
        }
        return File(pdfBytes, "application/pdf", $"transferencia-{transferId}.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("kardex-by-product")]
    public async Task<IActionResult> GetKardexByProductReport([FromQuery] KardexByProductReportQueryFilter filter)
    {
        var rows = await _reportService.GetKardexByProductReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("kardex-by-product/pdf")]
    public async Task<IActionResult> GetKardexByProductReportPdf([FromQuery] KardexByProductReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateKardexByProductReportPdf(filter);
        return File(pdfBytes, "application/pdf", "kardex-por-material.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("kardex-by-product/excel")]
    public async Task<IActionResult> GetKardexByProductReportExcel([FromQuery] KardexByProductReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateKardexByProductReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "kardex-por-material.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("kardex-by-product/txt")]
    public async Task<IActionResult> GetKardexByProductReportTxt([FromQuery] KardexByProductReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateKardexByProductReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "kardex-por-material.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("pastor-field/data")]
    public async Task<IActionResult> GetPastorFieldReportData([FromQuery] PastorFieldReportQueryFilter filter)
    {
        var data = await _reportService.GetPastorFieldReportData(filter);
        return Ok(data);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("pastor-field/pdf")]
    public async Task<IActionResult> GetPastorFieldReportPdf([FromQuery] PastorFieldReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GeneratePastorFieldReportPdf(filter);
        return File(pdfBytes, "application/pdf", "campo-pastor.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("pastor-field/excel")]
    public async Task<IActionResult> GetPastorFieldReportExcel([FromQuery] PastorFieldReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GeneratePastorFieldReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "campo-pastor.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("pastor-field/txt")]
    public async Task<IActionResult> GetPastorFieldReportTxt([FromQuery] PastorFieldReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GeneratePastorFieldReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "campo-pastor.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("account-receivables/data")]
    public async Task<IActionResult> GetAccountReceivablesReport([FromQuery] AccountReceivablesReportQueryFilter filter)
    {
        var rows = await _reportService.GetAccountReceivablesReport(filter);
        return Ok(rows);
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("account-receivables/pdf")]
    public async Task<IActionResult> GetAccountReceivablesReportPdf([FromQuery] AccountReceivablesReportQueryFilter filter)
    {
        var pdfBytes = await _reportService.GenerateAccountReceivablesReportPdf(filter);
        return File(pdfBytes, "application/pdf", "cuentas-por-cobrar.pdf");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("account-receivables/excel")]
    public async Task<IActionResult> GetAccountReceivablesReportExcel([FromQuery] AccountReceivablesReportQueryFilter filter)
    {
        var excelBytes = await _reportService.GenerateAccountReceivablesReportExcel(filter);
        return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "cuentas-por-cobrar.xlsx");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("account-receivables/txt")]
    public async Task<IActionResult> GetAccountReceivablesReportTxt([FromQuery] AccountReceivablesReportQueryFilter filter)
    {
        var txtBytes = await _reportService.ToTxt(() => _reportService.GenerateAccountReceivablesReportExcel(filter));
        return File(txtBytes, "text/plain; charset=utf-8", "cuentas-por-cobrar.txt");
    }

    [Authorize(Policy = "report.view")]
    [Authorize(Roles = "M-BOS,Tesorero,Contador,Auxiliar Contador")]
    [HttpGet("account-receivable-voucher/{accountReceivableId}/pdf")]
    public async Task<IActionResult> GetAccountReceivableVoucherPdf(long accountReceivableId)
    {
        var pdfBytes = await _reportService.GenerateAccountReceivableVoucherPdf(accountReceivableId);
        if (pdfBytes is null)
        {
            return NotFound();
        }
        return File(pdfBytes, "application/pdf", $"cuenta-por-cobrar-{accountReceivableId}.pdf");
    }
}
