using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.QueryFilters.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

// "Resumen por Departamento": solo el saldo final de cada departamento (cantidad de productos,
// unidades y valor), sin el detalle por producto. Mismos saldos que Stock - Departamento.
public partial class ReportService
{
    private sealed record DepartmentSummaryRow(string Department, int Products, decimal Quantity, decimal Value);

    private async Task<List<DepartmentSummaryRow>> GetDepartmentSummary(StockByDepartmentReportQueryFilter filter)
    {
        var rows = await GetStockByDepartmentReport(filter);
        return rows
            .GroupBy(r => r.DepartmentName ?? "(Sin departamento)")
            .Select(g => new DepartmentSummaryRow(g.Key, g.Count(), g.Sum(r => r.NetQuantity), g.Sum(r => r.TotalValue)))
            .OrderBy(r => r.Department, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    private async Task<string[]> DepartmentSummaryInfo(StockByDepartmentReportQueryFilter filter)
    {
        var info = new List<string> { $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}" };
        if (filter.DepartmentId.HasValue)
        {
            var department = await _unitOfWork.Repository<Department>().GetByIdAsync(filter.DepartmentId.Value);
            info.Add($"Departamento: {department?.Name ?? "—"}");
        }
        info.Add($"Saldo al: {BalanceDateText(filter.DateTo)}");
        info.Add($"Estado: {ReviewStates.Label(filter.ReviewState)}");
        return info.ToArray();
    }

    public async Task<byte[]> GenerateDepartmentSummaryPdf(StockByDepartmentReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetDepartmentSummary(filter);
        var info = await DepartmentSummaryInfo(filter);

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(9));

                page.Header().Element(c => ReportHeader(c, "Resumen por Departamento", info));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(28);
                        columns.RelativeColumn(3);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(80);
                        columns.ConstantColumn(100);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).AlignCenter().Text("N°");
                        header.Cell().Element(HeaderCell).Text("Departamento");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Productos");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldo (Bs.)");
                    });

                    if (rows.Count == 0)
                    {
                        table.Cell().ColumnSpan(5).Element(BodyCell).Text("Sin saldo en este almacén y fecha.").FontColor(ReportMuted);
                        return;
                    }

                    var number = 0;
                    foreach (var row in rows)
                    {
                        number++;
                        table.Cell().Element(BodyCell).AlignCenter().Text(number.ToString());
                        table.Cell().Element(BodyCell).Text(row.Department);
                        table.Cell().Element(BodyCell).AlignRight().Text(row.Products.ToString());
                        table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.Quantity));
                        table.Cell().Element(BodyCell).AlignRight().Text(row.Value.ToString("N2", ReportCulture));
                    }

                    table.Cell().ColumnSpan(2).Element(CategoryTotalCell).AlignRight().Text("Total General").Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(rows.Sum(r => r.Products).ToString()).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(rows.Sum(r => r.Quantity))).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(rows.Sum(r => r.Value).ToString("N2", ReportCulture)).Bold();
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Resumen por Departamento").GeneratePdf();
    }

    public async Task<byte[]> GenerateDepartmentSummaryExcel(StockByDepartmentReportQueryFilter filter)
    {
        var rows = await GetDepartmentSummary(filter);
        var info = (await DepartmentSummaryInfo(filter)).ToList();
        var columns = new List<ExcelColumn<DepartmentSummaryRow>>
        {
            new("Departamento", r => r.Department),
            new("Productos", r => r.Products, QuantityFormat, Total: true),
            new("Cantidad", r => r.Quantity, QuantityFormat, Total: true),
            new("Saldo (Bs.)", r => r.Value, MoneyFormat, Total: true)
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Resumen por departamento", "RESUMEN POR DEPARTAMENTO", info, columns, rows));
    }
}
