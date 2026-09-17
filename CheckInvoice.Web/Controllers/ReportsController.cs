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
    [HttpGet("issue-voucher/{issueId}/pdf")]
    public async Task<IActionResult> GetIssueVoucherPdf(long issueId, [FromQuery] long? printTypeId)
    {
        var pdfBytes = await _reportService.GenerateIssueVoucherPdf(issueId, printTypeId);
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
    [HttpGet("receipt-voucher/{receiptId}/pdf")]
    public async Task<IActionResult> GetReceiptVoucherPdf(long receiptId)
    {
        var pdfBytes = await _reportService.GenerateReceiptVoucherPdf(receiptId);
        if (pdfBytes is null)
        {
            return NotFound();
        }
        return File(pdfBytes, "application/pdf", $"ingreso-{receiptId}.pdf");
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
}
