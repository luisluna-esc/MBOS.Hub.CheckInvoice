using System.Globalization;
using System.Reflection;
using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.Application.Interfaces.Reports;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Reports;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

public partial class ReportService : IReportService
{
    private const int MonthsBack = 6;
    private static readonly CultureInfo ReportCulture = new("es-ES");

    /// <summary>Cantidades: son unidades enteras, así que van sin decimales (si alguna tuviera fracción, se muestra).</summary>
    private static string Qty(decimal quantity) => quantity.ToString("#,##0.##", ReportCulture);

    private static byte[]? _logoBytes;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ReportService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<FinancialDashboardDataDto> GetFinancialDashboardData()
    {
        var summary = await GetSummaryAsync();
        var issuesByMonth = await GetIssuesByMonthAsync();
        var receivableStatus = await GetReceivableStatusSummaryAsync();

        return new FinancialDashboardDataDto
        {
            Summary = summary,
            IssuesByMonth = issuesByMonth,
            ReceivableStatus = receivableStatus
        };
    }

    // EF Core mapea las columnas del resultado de FromSql/SqlQueryRaw por el nombre exacto
    // de la propiedad C# (ej. "ProductId"), no por el nombre de columna en snake_case que
    // devuelve la función SQL (ej. product_id) — de ahí los alias explícitos.
    private const string GetStockReportSql = """
        SELECT
            product_id AS "ProductId",
            code AS "Code",
            name AS "Name",
            unit_measure AS "UnitMeasure",
            department_id AS "DepartmentId",
            department_name AS "DepartmentName",
            sub_department_id AS "SubDepartmentId",
            sub_department_name AS "SubDepartmentName",
            net_quantity AS "NetQuantity",
            unit_value AS "UnitValue",
            total_value AS "TotalValue"
        FROM sp_get_stock_report({0}::bigint, {1}::date, {2}::date)
        """;

    public async Task<List<StockReportRowDto>> GetStockReport(StockReportQueryFilter filter)
    {
        return await _unitOfWork.SqlQueryAsync<StockReportRowDto>(
            GetStockReportSql,
            (object?)filter.WarehouseId ?? DBNull.Value,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);
    }

    public async Task<byte[]> GenerateStockReportPdf(StockReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetStockReport(filter);

        var warehouseName = "Todos los almacenes";
        if (filter.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(filter.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var groups = rows
            .GroupBy(r => r.DepartmentName ?? "(Sin categoría)")
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Department = g.Key,
                SubGroups = g.GroupBy(r => r.SubDepartmentName ?? "(Sin subcategoría)")
                    .OrderBy(sg => sg.Key)
                    .ToList()
            })
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(9));

                page.Header().Element(c => ReportHeader(c, "Inventario General", $"Almacén: {warehouseName}", $"Periodo: {periodText}"));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(55);
                        columns.RelativeColumn(3);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(80);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nTotal");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(6).Element(GroupTitleCell)
                            .Text(dept.Department).Bold().FontSize(10);

                        var deptTotal = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(6).Element(SubGroupTitleCell)
                                .Text(sub.Key).FontSize(9);

                            var subTotal = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name))
                            {
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.NetQuantity));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.UnitValue.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalValue.ToString("N2", ReportCulture));
                                subTotal += row.TotalValue;
                            }

                            table.Cell().ColumnSpan(5).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotal.ToString("N2", ReportCulture)).Bold();

                            deptTotal += subTotal;
                        }

                        table.Cell().ColumnSpan(5).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotal.ToString("N2", ReportCulture)).Bold();
                    }
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Inventario General").GeneratePdf();
    }

    // Plantilla en blanco para contar físicamente en papel, antes de que exista
    // ningún registro en inventory_count: usa el mismo saldo de sistema que
    // "Stock - Almacén" (sp_get_stock_report), pero deja Cantidad Física y
    // Diferencia vacías para llenar a mano. No crea ni lee inventory_count —
    // eso pasa después, al digitar el conteo en pantalla.
    public async Task<byte[]> GenerateInventoryCountTemplatePdf(StockReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetStockReport(filter);

        var warehouseName = "Todos los almacenes";
        if (filter.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(filter.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var groups = rows
            .GroupBy(r => r.DepartmentName ?? "(Sin categoría)")
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Department = g.Key,
                SubGroups = g.GroupBy(r => r.SubDepartmentName ?? "(Sin subcategoría)")
                    .OrderBy(sg => sg.Key)
                    .ToList()
            })
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(7));

                page.Header().Element(c => ReportHeader(c, "Plantilla de Conteo Físico", $"Almacén: {warehouseName}", $"Periodo: {periodText}"));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(32);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(44);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(44);
                        columns.ConstantColumn(46);
                        columns.ConstantColumn(42);
                        columns.ConstantColumn(44);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nTotal");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad\nFísica");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Diferencia");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(8).Element(GroupTitleCell)
                            .Text(dept.Department).Bold().FontSize(10);

                        var deptQty = 0m;
                        var deptTotalValue = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(8).Element(SubGroupTitleCell)
                                .Text(sub.Key).FontSize(9);

                            var subQty = 0m;
                            var subTotalValue = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name))
                            {
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.NetQuantity));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.UnitValue.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalValue.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).Text("");
                                table.Cell().Element(BodyCell).Text("");

                                subQty += row.NetQuantity;
                                subTotalValue += row.TotalValue;
                            }

                            table.Cell().ColumnSpan(3).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subQty)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotalValue.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).Text("");

                            deptQty += subQty;
                            deptTotalValue += subTotalValue;
                        }

                        table.Cell().ColumnSpan(3).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(deptQty)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotalValue.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).Text("");
                    }
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Plantilla de Conteo Físico").GeneratePdf();
    }

    private const string GetStockByDepartmentReportSql = """
        SELECT
            product_id AS "ProductId",
            code AS "Code",
            name AS "Name",
            unit_measure AS "UnitMeasure",
            department_id AS "DepartmentId",
            department_name AS "DepartmentName",
            sub_department_id AS "SubDepartmentId",
            sub_department_name AS "SubDepartmentName",
            net_quantity AS "NetQuantity",
            unit_value AS "UnitValue",
            total_value AS "TotalValue"
        FROM sp_get_stock_report_by_department({0}::bigint, {1}::bigint, {2}::date, {3}::date)
        """;

    public async Task<List<StockReportRowDto>> GetStockByDepartmentReport(StockByDepartmentReportQueryFilter filter)
    {
        return await _unitOfWork.SqlQueryAsync<StockReportRowDto>(
            GetStockByDepartmentReportSql,
            (object?)filter.WarehouseId ?? DBNull.Value,
            (object?)filter.DepartmentId ?? DBNull.Value,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);
    }

    public async Task<byte[]> GenerateStockByDepartmentReportPdf(StockByDepartmentReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetStockByDepartmentReport(filter);

        var warehouseName = "Todos los almacenes";
        if (filter.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(filter.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        var departmentName = "Todas las categorías";
        if (filter.DepartmentId.HasValue)
        {
            var department = await _unitOfWork.Repository<Department>().GetByIdAsync(filter.DepartmentId.Value);
            departmentName = department?.Name ?? departmentName;
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var groups = rows
            .GroupBy(r => r.SubDepartmentName ?? "(Sin subcategoría)")
            .OrderBy(g => g.Key)
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(9));

                page.Header().Element(c => ReportHeader(c, "Inventario por Departamento", $"Almacén: {warehouseName}", $"Categoría: {departmentName}", $"Periodo: {periodText}"));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(55);
                        columns.RelativeColumn(3);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(80);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nTotal");
                    });

                    var grandTotal = 0m;

                    foreach (var sub in groups)
                    {
                        table.Cell().ColumnSpan(6).Element(GroupTitleCell)
                            .Text(sub.Key).Bold().FontSize(10);

                        var subTotal = 0m;

                        foreach (var row in sub.OrderBy(r => r.Name))
                        {
                            table.Cell().Element(BodyCell).Text(row.Code ?? "");
                            table.Cell().Element(BodyCell).Text(row.Name);
                            table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.NetQuantity));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.UnitValue.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.TotalValue.ToString("N2", ReportCulture));
                            subTotal += row.TotalValue;
                        }

                        table.Cell().ColumnSpan(5).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subTotal.ToString("N2", ReportCulture)).Bold();

                        grandTotal += subTotal;
                    }

                    table.Cell().ColumnSpan(5).Element(CategoryTotalCell).AlignRight().Text("Total General").Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandTotal.ToString("N2", ReportCulture)).Bold();
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Inventario por Departamento").GeneratePdf();
    }

    private const string GetKardexReportSql = """
        SELECT
            product_id AS "ProductId",
            code AS "Code",
            name AS "Name",
            sub_department_id AS "SubDepartmentId",
            sub_department_name AS "SubDepartmentName",
            opening_quantity AS "OpeningQuantity",
            opening_value AS "OpeningValue",
            receipts_quantity AS "ReceiptsQuantity",
            receipts_value AS "ReceiptsValue",
            issues_quantity AS "IssuesQuantity",
            issues_value AS "IssuesValue",
            closing_quantity AS "ClosingQuantity",
            closing_value AS "ClosingValue"
        FROM sp_get_kardex_report({0}::bigint, {1}::date, {2}::date)
        """;

    public async Task<List<KardexReportRowDto>> GetKardexReport(KardexReportQueryFilter filter)
    {
        return await _unitOfWork.SqlQueryAsync<KardexReportRowDto>(
            GetKardexReportSql,
            (object?)filter.WarehouseId ?? DBNull.Value,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);
    }

    public async Task<byte[]> GenerateKardexReportPdf(KardexReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetKardexReport(filter);

        var warehouseName = "Todos los almacenes";
        if (filter.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(filter.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var groups = rows
            .GroupBy(r => r.SubDepartmentName ?? "(Sin subcategoría)")
            .OrderBy(g => g.Key)
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(8));

                page.Header().Element(c => ReportHeader(c, "Kardex Físico Valorado", $"Almacén: {warehouseName}", $"Periodo: {periodText}"));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(45);
                        columns.RelativeColumn(2);
                        columns.ConstantColumn(64);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(58);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(58);
                        columns.ConstantColumn(65);
                        columns.ConstantColumn(58);
                        columns.ConstantColumn(65);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldo Inicial\nCant.");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldo Inicial\nCosto");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Entradas\nCant.");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Entradas\nCosto");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Salidas\nCant.");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Salidas\nCosto");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldos\nCant.");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldos\nCosto");
                    });

                    decimal grandReceiptsQty = 0m, grandReceiptsValue = 0m;
                    decimal grandIssuesQty = 0m, grandIssuesValue = 0m;
                    decimal grandClosingQty = 0m, grandClosingValue = 0m;

                    foreach (var sub in groups)
                    {
                        table.Cell().ColumnSpan(10).Element(GroupTitleCell)
                            .Text(sub.Key).Bold().FontSize(10);

                        decimal subReceiptsQty = 0m, subReceiptsValue = 0m;
                        decimal subIssuesQty = 0m, subIssuesValue = 0m;
                        decimal subClosingQty = 0m, subClosingValue = 0m;

                        foreach (var row in sub.OrderBy(r => r.Name))
                        {
                            table.Cell().Element(BodyCell).Text(row.Code ?? "");
                            table.Cell().Element(BodyCell).Text(row.Name);
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.OpeningQuantity));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.OpeningValue.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.ReceiptsQuantity));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.ReceiptsValue.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.IssuesQuantity));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.IssuesValue.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.ClosingQuantity));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.ClosingValue.ToString("N2", ReportCulture));

                            subReceiptsQty += row.ReceiptsQuantity;
                            subReceiptsValue += row.ReceiptsValue;
                            subIssuesQty += row.IssuesQuantity;
                            subIssuesValue += row.IssuesValue;
                            subClosingQty += row.ClosingQuantity;
                            subClosingValue += row.ClosingValue;
                        }

                        table.Cell().ColumnSpan(2).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                        table.Cell().Element(SubtotalCell).Text("");
                        table.Cell().Element(SubtotalCell).Text("");
                        table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subReceiptsQty)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subReceiptsValue.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subIssuesQty)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subIssuesValue.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subClosingQty)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subClosingValue.ToString("N2", ReportCulture)).Bold();

                        grandReceiptsQty += subReceiptsQty;
                        grandReceiptsValue += subReceiptsValue;
                        grandIssuesQty += subIssuesQty;
                        grandIssuesValue += subIssuesValue;
                        grandClosingQty += subClosingQty;
                        grandClosingValue += subClosingValue;
                    }

                    table.Cell().ColumnSpan(2).Element(CategoryTotalCell).AlignRight().Text("Total General").Bold();
                    table.Cell().Element(CategoryTotalCell).Text("");
                    table.Cell().Element(CategoryTotalCell).Text("");
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(grandReceiptsQty)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandReceiptsValue.ToString("N2", ReportCulture)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(grandIssuesQty)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandIssuesValue.ToString("N2", ReportCulture)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(grandClosingQty)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandClosingValue.ToString("N2", ReportCulture)).Bold();
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Kardex Físico Valorado").GeneratePdf();
    }

    private const string GetInventoryCountReportSql = """
        SELECT
            product_id AS "ProductId",
            code AS "Code",
            name AS "Name",
            unit_measure AS "UnitMeasure",
            department_id AS "DepartmentId",
            department_name AS "DepartmentName",
            sub_department_id AS "SubDepartmentId",
            sub_department_name AS "SubDepartmentName",
            quantity AS "Quantity",
            unit_value AS "UnitValue",
            total_value AS "TotalValue",
            physical_quantity AS "PhysicalQuantity",
            difference AS "Difference"
        FROM sp_get_inventory_count_report({0}::bigint, {1}::date, {2}::date)
        """;

    public async Task<List<InventoryCountReportRowDto>> GetInventoryCountReport(InventoryCountReportQueryFilter filter)
    {
        return await _unitOfWork.SqlQueryAsync<InventoryCountReportRowDto>(
            GetInventoryCountReportSql,
            (object?)filter.WarehouseId ?? DBNull.Value,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);
    }

    public async Task<byte[]> GenerateInventoryCountReportPdf(InventoryCountReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetInventoryCountReport(filter);

        var warehouseName = "Todos los almacenes";
        if (filter.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(filter.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var groups = rows
            .GroupBy(r => r.DepartmentName ?? "(Sin categoría)")
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Department = g.Key,
                SubGroups = g.GroupBy(r => r.SubDepartmentName ?? "(Sin subcategoría)")
                    .OrderBy(sg => sg.Key)
                    .ToList()
            })
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(7));

                page.Header().Element(c => ReportHeader(c, "Levantamiento de Inventario", $"Almacén: {warehouseName}", $"Periodo: {periodText}"));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(32);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(44);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(44);
                        columns.ConstantColumn(46);
                        columns.ConstantColumn(42);
                        columns.ConstantColumn(44);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nTotal");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad\nFísica");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Diferencia");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(8).Element(GroupTitleCell)
                            .Text(dept.Department).Bold().FontSize(10);

                        decimal deptQty = 0m, deptTotalValue = 0m, deptPhysicalQty = 0m, deptDifference = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(8).Element(SubGroupTitleCell)
                                .Text(sub.Key).FontSize(9);

                            decimal subQty = 0m, subTotalValue = 0m, subPhysicalQty = 0m, subDifference = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name))
                            {
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.Quantity));
                                table.Cell().Element(BodyCell).AlignRight().Text((row.UnitValue ?? 0).ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalValue.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(Qty((row.PhysicalQuantity ?? 0)));
                                table.Cell().Element(BodyCell).AlignRight().Text(Qty((row.Difference ?? 0)));

                                subQty += row.Quantity;
                                subTotalValue += row.TotalValue;
                                subPhysicalQty += row.PhysicalQuantity ?? 0;
                                subDifference += row.Difference ?? 0;
                            }

                            table.Cell().ColumnSpan(3).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subQty)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotalValue.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subPhysicalQty)).Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subDifference)).Bold();

                            deptQty += subQty;
                            deptTotalValue += subTotalValue;
                            deptPhysicalQty += subPhysicalQty;
                            deptDifference += subDifference;
                        }

                        table.Cell().ColumnSpan(3).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(deptQty)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotalValue.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(deptPhysicalQty)).Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(deptDifference)).Bold();
                    }
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Levantamiento de Inventario").GeneratePdf();
    }

    private const string GetIssuesReportSql = """
        SELECT
            issue_id AS "IssueId",
            product_id AS "ProductId",
            code AS "Code",
            name AS "Name",
            unit_measure AS "UnitMeasure",
            department_id AS "DepartmentId",
            department_name AS "DepartmentName",
            sub_department_id AS "SubDepartmentId",
            sub_department_name AS "SubDepartmentName",
            quantity AS "Quantity",
            unit_cost AS "UnitCost",
            total_cost AS "TotalCost"
        FROM sp_get_issues_report({0}::bigint, {1}::date, {2}::date)
        """;

    public async Task<List<IssuesReportRowDto>> GetIssuesReport(IssuesReportQueryFilter filter)
    {
        await LoadIssueNumbersAsync();
        return await _unitOfWork.SqlQueryAsync<IssuesReportRowDto>(
            GetIssuesReportSql,
            (object?)filter.WarehouseId ?? DBNull.Value,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);
    }

    public async Task<byte[]> GenerateIssuesReportPdf(IssuesReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetIssuesReport(filter);

        var warehouseName = "Todos los almacenes";
        if (filter.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(filter.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var groups = rows
            .GroupBy(r => r.DepartmentName ?? "(Sin categoría)")
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Department = g.Key,
                SubGroups = g.GroupBy(r => r.SubDepartmentName ?? "(Sin subcategoría)")
                    .OrderBy(sg => sg.Key)
                    .ToList()
            })
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(7));

                page.Header().Element(c => ReportHeader(c, "Salidas de Almacén", $"Almacén: {warehouseName}", $"Periodo: {periodText}"));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(32);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(42);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(42);
                        columns.ConstantColumn(45);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("N°");
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nTotal");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(7).Element(GroupTitleCell)
                            .Text(dept.Department).Bold().FontSize(10);

                        decimal deptQty = 0m, deptTotalValue = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(7).Element(SubGroupTitleCell)
                                .Text(sub.Key).FontSize(9);

                            decimal subQty = 0m, subTotalValue = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name).ThenBy(r => r.IssueId))
                            {
                                table.Cell().Element(BodyCell).Text(IssueNo(row.IssueId));
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.Quantity));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.UnitCost.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalCost.ToString("N2", ReportCulture));

                                subQty += row.Quantity;
                                subTotalValue += row.TotalCost;
                            }

                            table.Cell().ColumnSpan(4).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subQty)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotalValue.ToString("N2", ReportCulture)).Bold();

                            deptQty += subQty;
                            deptTotalValue += subTotalValue;
                        }

                        table.Cell().ColumnSpan(4).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(deptQty)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotalValue.ToString("N2", ReportCulture)).Bold();
                    }
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Salidas de Almacén").GeneratePdf();
    }

    private const string GetReceiptsReportSql = """
        SELECT
            receipt_id AS "ReceiptId",
            receipt_date AS "ReceiptDate",
            product_id AS "ProductId",
            code AS "Code",
            name AS "Name",
            unit_measure AS "UnitMeasure",
            department_id AS "DepartmentId",
            department_name AS "DepartmentName",
            sub_department_id AS "SubDepartmentId",
            sub_department_name AS "SubDepartmentName",
            quantity AS "Quantity",
            unit_cost AS "UnitCost",
            total_cost AS "TotalCost"
        FROM sp_get_receipts_report({0}::bigint, {1}::date, {2}::date)
        """;

    public async Task<List<ReceiptsReportRowDto>> GetReceiptsReport(ReceiptsReportQueryFilter filter)
    {
        return await _unitOfWork.SqlQueryAsync<ReceiptsReportRowDto>(
            GetReceiptsReportSql,
            (object?)filter.WarehouseId ?? DBNull.Value,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);
    }

    public async Task<byte[]> GenerateReceiptsReportPdf(ReceiptsReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetReceiptsReport(filter);
        var noteNumbers = await ReceiptNoteNumbersAsync(rows.Select(r => r.ReceiptId));

        var warehouseName = "Todos los almacenes";
        if (filter.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(filter.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var groups = rows
            .GroupBy(r => r.DepartmentName ?? "(Sin categoría)")
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Department = g.Key,
                SubGroups = g.GroupBy(r => r.SubDepartmentName ?? "(Sin subcategoría)")
                    .OrderBy(sg => sg.Key)
                    .ToList()
            })
            .ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(7));

                page.Header().Element(c => ReportHeader(c, "Ingresos de Almacén", $"Almacén: {warehouseName}", $"Periodo: {periodText}"));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(58);
                        columns.ConstantColumn(45);
                        columns.ConstantColumn(30);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(38);
                        columns.ConstantColumn(40);
                        columns.ConstantColumn(42);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("N° Nota");
                        header.Cell().Element(HeaderCell).Text("Fecha");
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nTotal");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(8).Element(GroupTitleCell)
                            .Text(dept.Department).Bold().FontSize(10);

                        decimal deptQty = 0m, deptTotalValue = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(8).Element(SubGroupTitleCell)
                                .Text(sub.Key).FontSize(9);

                            decimal subQty = 0m, subTotalValue = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name).ThenBy(r => r.ReceiptId))
                            {
                                table.Cell().Element(BodyCell).Text(noteNumbers.GetValueOrDefault(row.ReceiptId, row.ReceiptId.ToString()));
                                table.Cell().Element(BodyCell).Text(row.ReceiptDate.ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.Quantity));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.UnitCost.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalCost.ToString("N2", ReportCulture));

                                subQty += row.Quantity;
                                subTotalValue += row.TotalCost;
                            }

                            table.Cell().ColumnSpan(5).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(subQty)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotalValue.ToString("N2", ReportCulture)).Bold();

                            deptQty += subQty;
                            deptTotalValue += subTotalValue;
                        }

                        table.Cell().ColumnSpan(5).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(deptQty)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotalValue.ToString("N2", ReportCulture)).Bold();
                    }
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Ingresos de Almacén").GeneratePdf();
    }

    private const string GetIssueVoucherLinesSql = """
        SELECT
            code AS "Code",
            name AS "Name",
            unit_measure AS "UnitMeasure",
            department_name AS "DepartmentName",
            quantity AS "Quantity",
            unit_cost AS "UnitCost",
            total_cost AS "TotalCost"
        FROM sp_get_issue_voucher_lines({0}::bigint)
        """;

    /// <param name="publicBaseUrl">Dirección del sistema: con ella el comprobante en hoja lleva un QR que
    /// abre la salida con sus fotos y firmas (igual que la nota de ingreso).</param>
    public async Task<byte[]?> GenerateIssueVoucherPdf(long issueId, long? printTypeId = null, string? publicBaseUrl = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId);
        if (issue is null)
        {
            return null;
        }

        var lines = await _unitOfWork.SqlQueryAsync<IssueVoucherLineDto>(GetIssueVoucherLinesSql, issueId);

        var warehouseName = "—";
        if (issue.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(issue.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        // El nombre del cliente vive en Party (Client.PartyId -> Party.Name), igual que
        // Supplier.LegalName resuelve al proveedor en el comprobante de Entrada.
        var clientName = "—";
        if (issue.ClientId.HasValue)
        {
            var client = await _unitOfWork.Repository<Client>().GetByIdAsync(issue.ClientId.Value);
            if (client is not null)
            {
                var party = await _unitOfWork.Repository<Party>().GetByIdAsync(client.PartyId);
                clientName = party?.Name ?? clientName;
            }
        }

        // printTypeId permite reimprimir una Salida ya guardada en un formato distinto al que
        // se eligió al crearla (p.ej. se guardó como "Nota de Entrega" pero el cliente ahora
        // pide una "Factura") — si no se pasa, se usa el tipo guardado en la Salida.
        var printTypeName = "—";
        var effectivePrintTypeId = printTypeId ?? issue.PrintTypeId;
        if (effectivePrintTypeId.HasValue)
        {
            var printType = await _unitOfWork.Repository<PrintType>().GetByIdAsync(effectivePrintTypeId.Value);
            printTypeName = printType?.Name ?? printTypeName;
        }

        var issueTypeName = "—";
        if (issue.IssueTypeId.HasValue)
        {
            var issueType = await _unitOfWork.Repository<IssueType>().GetByIdAsync(issue.IssueTypeId.Value);
            issueTypeName = issueType?.Name ?? issueTypeName;
        }

        // "Responsable de Inventarios" se preimprime con el usuario que registró la salida en
        // el sistema. "Recibí Conforme" es el Cliente (quien recibe el material), a diferencia
        // del comprobante de Entrada donde ese rol lo cumple quien registró el ingreso.
        var inventoryManagerName = "—";
        if (issue.CreatedById.HasValue)
        {
            var creator = await _unitOfWork.Repository<AppUser>().GetByIdAsync(issue.CreatedById.Value);
            if (creator is not null)
            {
                inventoryManagerName = $"{creator.FirstName} {creator.LastName}".Trim();
            }
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var total = lines.Sum(l => l.TotalCost);

        // "Impresion Rollo" se imprime en rollo térmico angosto (como ya se hace en el negocio
        // con ventas al contado) — cualquier otro tipo ("Impresion Hoja", etc.) usa el formato
        // de página completa. Por decisión explícita del cliente, el rollo NO incluye
        // NIT/N° Autorización/Código de Control: esos son datos de facturación fiscal oficial
        // (SIN) que este sistema no gestiona, y no deben inventarse.
        var isRoll = printTypeName.Equals("Impresion Rollo", StringComparison.OrdinalIgnoreCase);

        var signatures = await LoadSignaturesAsync(DocumentReviews.Issue, issueId, issue.ReviewVersion);

        var document = isRoll
            ? BuildIssueVoucherRollDocument(issueId, printTypeName, issue, lines, total, warehouseName, clientName, username)
            : BuildIssueVoucherFullPageDocument(issueId, printTypeName, issueTypeName, issue, lines, total, warehouseName, clientName, inventoryManagerName, username, signatures,
                string.IsNullOrWhiteSpace(publicBaseUrl) ? null : QrPng($"{publicBaseUrl.TrimEnd('/')}/issues/view/{issueId}"));

        return WithTitle(document, $"N° {IssueNumbering.Format(issue.MonthlyNumber, issue.IssueDate, issueId)}").GeneratePdf();
    }

    private static Document BuildIssueVoucherFullPageDocument(
        long issueId,
        string printTypeName,
        string issueTypeName,
        Issue issue,
        List<IssueVoucherLineDto> lines,
        decimal total,
        string warehouseName,
        string clientName,
        string inventoryManagerName,
        string username,
        DocumentSignatures signatures,
        byte[]? qr)
    {
        // Media carta, como la nota de ingreso. "Recibí conforme" es el cliente; "Responsable de
        // inventarios", quien registró la salida.
        return BuildHalfLetterVoucher(new HalfLetterVoucher(
            Title: "SALIDA DE ALMACÉN",
            NumberLabel: "N°",
            NumberText: IssueNumbering.Format(issue.MonthlyNumber, issue.IssueDate, issueId),
            NumberSubText: null,
            InfoRows:
            [
                [("ALMACÉN", warehouseName), ("FECHA", issue.IssueDate.ToString("dd/MM/yyyy"))],
                [("CLIENTE", clientName), ("TIPO DE SALIDA", issueTypeName)],
            ],
            Note: issue.Description,
            Columns:
            [
                new VoucherColumn("CÓDIGO", 50),
                new VoucherColumn("DESCRIPCIÓN", null),
                new VoucherColumn("UNIDAD", 54, AlignCenter: true),
                new VoucherColumn("CANTIDAD", 46, AlignRight: true),
                new VoucherColumn("COSTO\nUNIT. (Bs.)", 56, AlignRight: true),
                new VoucherColumn("COSTO\nTOTAL (Bs.)", 62, AlignRight: true),
            ],
            Rows: lines.Select(line => new VoucherRow(
            [
                line.Code ?? "—",
                line.Name,
                line.UnitMeasure ?? "—",
                Qty(line.Quantity),
                line.UnitCost.ToString("N2", ReportCulture),
                line.TotalCost.ToString("N2", ReportCulture),
            ], line.TotalCost)).ToList(),
            TotalLabel: "TOTAL (Bs.)",
            RowsOnLastPage: 30,
            Signatures: column => DeliveryAndDigitalSignatures(
                column, clientName, "RECIBÍ CONFORME", inventoryManagerName, "RESPONSABLE DE INVENTARIOS", signatures),
            PrintedBy: username,
            Qr: qr));
    }

    // Rollo térmico angosto (80mm) para "Impresion Rollo" — solo el estilo visual del ejemplo
    // en papel que ya usa el negocio, sin los campos de facturación fiscal oficial (NIT propio,
    // N° Autorización, Código de Control) que ese ejemplo tenía, porque este sistema no emite
    // facturas fiscales reales ante el SIN.
    private static Document BuildIssueVoucherRollDocument(
        long issueId,
        string printTypeName,
        Issue issue,
        List<IssueVoucherLineDto> lines,
        decimal total,
        string warehouseName,
        string clientName,
        string username)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.ContinuousSize(227);
                page.MarginHorizontal(10);
                page.MarginVertical(8);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(8));

                page.Header().Column(column =>
                {
                    column.Spacing(2);
                    column.Item().AlignCenter().Height(30).Image(GetLogoBytes()).FitArea();
                    column.Item().AlignCenter().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(8).AlignCenter();
                    column.Item().AlignCenter().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").FontSize(7).AlignCenter();
                    column.Item().AlignCenter().Text("LA PAZ - BOLIVIA").FontSize(7).AlignCenter();
                    column.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Black);
                    column.Item().PaddingTop(2).AlignCenter().Text(printTypeName.ToUpperInvariant()).Bold().FontSize(12);
                    column.Item().PaddingBottom(2).LineHorizontal(1).LineColor(Colors.Black);
                });

                page.Content().Column(mainColumn =>
                {
                    mainColumn.Spacing(2);

                    mainColumn.Item().Text(t =>
                    {
                        t.Span("N° ").Bold();
                        t.Span(IssueNumbering.Format(issue.MonthlyNumber, issue.IssueDate, issueId));
                    });
                    mainColumn.Item().Text(t =>
                    {
                        t.Span("Almacén: ").Bold();
                        t.Span(warehouseName);
                    });
                    mainColumn.Item().Text(t =>
                    {
                        t.Span("Fecha: ").Bold();
                        t.Span(issue.IssueDate.ToString("dd/MM/yyyy"));
                    });
                    mainColumn.Item().Text(t =>
                    {
                        t.Span("Cliente: ").Bold();
                        t.Span(clientName);
                    });

                    mainColumn.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);

                    mainColumn.Item().PaddingTop(2).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.RelativeColumn(1);
                            columns.ConstantColumn(38);
                            columns.ConstantColumn(45);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Text("Cant.").Bold().FontSize(7);
                            header.Cell().Text("Detalle").Bold().FontSize(7);
                            header.Cell().AlignRight().Text("P.Unit.").Bold().FontSize(7);
                            header.Cell().AlignRight().Text("Subtotal").Bold().FontSize(7);
                        });

                        foreach (var line in lines)
                        {
                            table.Cell().PaddingTop(1).Text(Qty(line.Quantity)).FontSize(7);
                            table.Cell().PaddingTop(1).Text(line.Name).FontSize(7);
                            table.Cell().PaddingTop(1).AlignRight().Text(line.UnitCost.ToString("N2", ReportCulture)).FontSize(7);
                            table.Cell().PaddingTop(1).AlignRight().Text(line.TotalCost.ToString("N2", ReportCulture)).FontSize(7);
                        }
                    });

                    mainColumn.Item().PaddingTop(4).LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);

                    mainColumn.Item().PaddingTop(3).Row(row =>
                    {
                        row.RelativeItem().Text("TOTAL Bs.").Bold();
                        row.ConstantItem(55).AlignRight().Text(total.ToString("N2", ReportCulture)).Bold();
                    });

                    mainColumn.Item().PaddingTop(4).Text(
                        $"SON: {NumberToSpanishWords((long)decimal.Truncate(total))} {(int)((total - decimal.Truncate(total)) * 100):00}/100 BOLIVIANOS"
                    ).FontSize(7).Italic();

                    // Dos espacios de firma apilados (el rollo mide 80mm de ancho, no hay lugar
                    // para ponerlos lado a lado como en la versión de página completa) — el ancho
                    // de la página no cambia, solo crece el alto porque page.ContinuousSize() ya
                    // se ajusta automáticamente al contenido.
                    mainColumn.Item().PaddingTop(10).Column(c => RollSignatureLine(c, null));
                    mainColumn.Item().PaddingTop(8).Column(c => RollSignatureLine(c, clientName));

                    mainColumn.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Black);
                    mainColumn.Item().PaddingTop(3).AlignCenter().Text("Comprobante interno de salida de almacén.").FontSize(6);
                    mainColumn.Item().AlignCenter().Text("No es válido como factura fiscal.").FontSize(6);
                });

                page.Footer().PaddingTop(4).Column(c =>
                {
                    c.Item().AlignCenter().Text($"Usuario: {username}").FontSize(6);
                    c.Item().AlignCenter().Text($"{BoliviaTime.Now:dd/MM/yyyy HH:mm}").FontSize(6);
                });
            });
        });
    }

    // Convierte un entero (parte de Bolivianos, sin centavos) a palabras en español para la
    // línea "SON: ... BOLIVIANOS" del rollo — los centavos se muestran como dígitos (00/100),
    // igual que en el formato boliviano estándar (ver ejemplo en papel: "DIEZ 00/100 BOLIVIANOS").
    private static string NumberToSpanishWords(long number)
    {
        if (number == 0)
        {
            return "CERO";
        }

        if (number < 0)
        {
            return "MENOS " + NumberToSpanishWords(-number);
        }

        if (number >= 1_000_000)
        {
            var millions = number / 1_000_000;
            var remainder = number % 1_000_000;
            var millionsText = millions == 1 ? "UN MILLÓN" : $"{NumberToSpanishWords(millions)} MILLONES";
            return remainder == 0 ? millionsText : $"{millionsText} {NumberToSpanishWords(remainder)}";
        }

        if (number >= 1000)
        {
            var thousands = number / 1000;
            var remainder = number % 1000;
            var thousandsText = thousands == 1 ? "MIL" : $"{NumberToSpanishWords(thousands)} MIL";
            return remainder == 0 ? thousandsText : $"{thousandsText} {NumberToSpanishWords(remainder)}";
        }

        string[] units = ["", "UNO", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE"];
        string[] teens = ["DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS", "DIECISIETE", "DIECIOCHO", "DIECINUEVE"];
        string[] tens = ["", "", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA", "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"];
        string[] hundreds = ["", "CIENTO", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS", "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"];

        if (number == 100)
        {
            return "CIEN";
        }

        var parts = new List<string>();

        if (number >= 100)
        {
            parts.Add(hundreds[number / 100 % 10]);
            number %= 100;
        }

        if (number >= 20)
        {
            var tensPart = tens[number / 10];
            var unitsDigit = number % 10;
            parts.Add(unitsDigit == 0 ? tensPart : $"{tensPart} Y {units[unitsDigit]}");
        }
        else if (number >= 10)
        {
            parts.Add(teens[number - 10]);
        }
        else if (number > 0)
        {
            parts.Add(units[number]);
        }

        return string.Join(" ", parts);
    }

    private const string GetTransferVoucherLinesSql = """
        SELECT
            code AS "Code",
            name AS "Name",
            unit_measure AS "UnitMeasure",
            department_name AS "DepartmentName",
            quantity AS "Quantity",
            unit_price AS "UnitPrice",
            total_sale_price AS "TotalSalePrice"
        FROM sp_get_transfer_voucher_lines({0}::bigint)
        """;

    public async Task<byte[]?> GenerateTransferVoucherPdf(long transferId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var transfer = await _unitOfWork.Repository<Transfer>().GetByIdAsync(transferId);
        if (transfer is null)
        {
            return null;
        }

        var lines = await _unitOfWork.SqlQueryAsync<TransferVoucherLineDto>(GetTransferVoucherLinesSql, transferId);

        var sourceWarehouseName = "—";
        if (transfer.SourceWarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(transfer.SourceWarehouseId.Value);
            sourceWarehouseName = warehouse?.Name ?? sourceWarehouseName;
        }

        var destinationWarehouseName = "—";
        if (transfer.DestinationWarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(transfer.DestinationWarehouseId.Value);
            destinationWarehouseName = warehouse?.Name ?? destinationWarehouseName;
        }

        var senderName = "—";
        var senderEmail = "—";
        if (transfer.SenderUserId.HasValue)
        {
            var sender = await _unitOfWork.Repository<AppUser>().GetByIdAsync(transfer.SenderUserId.Value);
            if (sender is not null)
            {
                senderName = $"{sender.FirstName} {sender.LastName}".Trim();
                senderEmail = sender.Email;
            }
        }

        // Las transferencias son internas, entre almacenes propios: ya no hay un receptor elegido.
        // La firma de quien recibe queda en blanco para el encargado del almacén de destino.
        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var total = lines.Sum(l => l.TotalSalePrice);
        var signatures = await LoadSignaturesAsync(DocumentReviews.Transfer, transferId, transfer.ReviewVersion);

        // Media carta, como la nota de ingreso. Orden pedido: cantidad primero, luego descripción;
        // sin código y con costos (no precios de venta).
        var document = BuildHalfLetterVoucher(new HalfLetterVoucher(
            Title: "TRANSFERENCIA DE ALMACÉN",
            NumberLabel: "TRANSFERENCIA N°",
            NumberText: transferId.ToString().PadLeft(5, '0'),
            NumberSubText: null,
            InfoRows:
            [
                [("ORIGEN", sourceWarehouseName), ("DESTINO", destinationWarehouseName), ("FECHA", BoliviaTime.FromUtc(transfer.TransferDate).ToString("dd/MM/yyyy"))],
                [("ENVÍA", senderName), ("CORREO", senderEmail)],
            ],
            Note: transfer.Notes,
            Columns:
            [
                new VoucherColumn("CANTIDAD", 50, AlignRight: true),
                new VoucherColumn("UNIDAD", 54, AlignCenter: true),
                new VoucherColumn("DESCRIPCIÓN", null),
                new VoucherColumn("DEPT.", 80),
                new VoucherColumn("COSTO\nUNIT. (Bs.)", 56, AlignRight: true),
                new VoucherColumn("COSTO\nTOTAL (Bs.)", 62, AlignRight: true),
            ],
            Rows: lines.Select(line => new VoucherRow(
            [
                Qty(line.Quantity),
                line.UnitMeasure ?? "—",
                line.Name,
                line.DepartmentName ?? "—",
                line.UnitPrice.ToString("N2", ReportCulture),
                line.TotalSalePrice.ToString("N2", ReportCulture),
            ], line.TotalSalePrice)).ToList(),
            TotalLabel: "TOTAL (Bs.)",
            RowsOnLastPage: 30,
            Signatures: column => DeliveryAndDigitalSignatures(
                column, senderName, "ENTREGADO POR", null, "RECIBIDO POR (ALMACÉN DE DESTINO)", signatures),
            PrintedBy: username,
            Qr: null));

        return WithTitle(document, $"Transferencia N° {transferId.ToString().PadLeft(5, '0')}").GeneratePdf();
    }

    // "late" no se guarda en account_receivable.status (ver sp_get_account_receivables en
    // storedProcedures.sql, que ya devuelve el estado efectivo) — este helper replica el mismo
    // criterio para el comprobante individual, que lee la entidad directo por EF en vez de
    // pasar por ese stored procedure.
    private static string EffectiveAccountReceivableStatus(string status, DateOnly? dueDate) =>
        AccountReceivableStatus.Effective(status, dueDate);

    private static string AccountReceivableStatusLabel(string status) => status switch
    {
        "paid" => "Pagado",
        "late" => "Pago Retrasado",
        _ => "Pendiente"
    };

    // Payment.PaymentMethod se guarda como 'cash'/'transfer' (el formulario ya no deja texto
    // libre) — valores viejos de antes de ese cambio caen al else y se muestran tal cual.
    private static string PaymentMethodLabel(string? paymentMethod) => paymentMethod switch
    {
        "cash" => "Efectivo",
        "transfer" => "Transacción",
        null or "" => "—",
        _ => paymentMethod
    };

    // Igual que SignatureBox (línea y nombre debajo) pero a tamaño reducido (fuente/alto),
    // para que quepa en el ancho angosto del rollo térmico (80mm).
    private static void RollSignatureLine(QuestPDF.Fluent.ColumnDescriptor column, string? name)
    {
        column.Item().BorderTop(1).BorderColor(Colors.Grey.Darken1).PaddingTop(2).AlignCenter().Text(name ?? "").FontSize(7);
    }

    private const string GetKardexByProductReportSql = """
        SELECT
            movement_type AS "MovementType",
            voucher_id AS "VoucherId",
            voucher_date AS "VoucherDate",
            quantity AS "Quantity",
            unit_cost AS "UnitCost",
            total_cost AS "TotalCost",
            balance_quantity AS "BalanceQuantity",
            balance_value AS "BalanceValue"
        FROM sp_get_kardex_by_product_report({0}::bigint, {1}::bigint, {2}::date, {3}::date)
        """;

    public async Task<List<KardexByProductReportRowDto>> GetKardexByProductReport(KardexByProductReportQueryFilter filter)
    {
        var sections = await GetKardexByProductSections(filter);
        return sections.SelectMany(s => s.Rows).ToList();
    }

    private async Task<List<KardexByProductReportRowDto>> GetKardexByProductRows(long productId, KardexByProductReportQueryFilter filter) =>
        await _unitOfWork.SqlQueryAsync<KardexByProductReportRowDto>(
            GetKardexByProductReportSql,
            productId,
            filter.WarehouseId,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);

    // Un bloque del kardex por material: el producto, su saldo inicial y sus movimientos.
    private sealed record KardexByProductSection(
        string ProductLabel, KardexByProductOpeningBalanceDto Opening, List<KardexByProductReportRowDto> Rows);

    // Con un producto, su bloque; sin producto ("Todos los productos"), un bloque por cada producto
    // que pasó por el almacén y tiene saldo o movimientos en el periodo, por nombre.
    private async Task<List<KardexByProductSection>> GetKardexByProductSections(KardexByProductReportQueryFilter filter)
    {
        var productIds = filter.ProductId.HasValue
            ? [filter.ProductId.Value]
            : await _unitOfWork.Repository<Stock>().Query()
                .Where(s => s.WarehouseId == filter.WarehouseId)
                .Select(s => s.ProductId)
                .ToListAsync();
        var products = await _unitOfWork.Repository<Product>().Query()
            .Where(p => productIds.Contains(p.ProductId))
            .OrderBy(p => p.Name)
            .Select(p => new { p.ProductId, p.Code, p.Name })
            .ToListAsync();

        var sections = new List<KardexByProductSection>();
        foreach (var product in products)
        {
            var rows = await GetKardexByProductRows(product.ProductId, filter);
            var opening = await GetKardexByProductOpeningBalance(product.ProductId, filter.WarehouseId, filter.DateFrom);
            if (!filter.ProductId.HasValue && rows.Count == 0 && opening.OpeningQuantity == 0)
            {
                continue;
            }
            sections.Add(new KardexByProductSection($"{product.Code} - {product.Name}", opening, rows));
        }
        return sections;
    }

    /// <summary>N° de comprobante de cada movimiento: "I 3-10-26" las entradas y "S 5-10-26" las salidas.</summary>
    private async Task<Func<KardexByProductReportRowDto, string>> KardexVoucherLabels(IEnumerable<KardexByProductReportRowDto> rows)
    {
        var list = rows.ToList();
        var noteNumbers = await ReceiptNoteNumbersAsync(list.Where(r => r.MovementType == "receipt").Select(r => r.VoucherId));
        var issueNumbers = await IssueNumbering.NumbersAsync(_unitOfWork, list.Where(r => r.MovementType != "receipt").Select(r => r.VoucherId));
        return row => row.MovementType == "receipt"
            ? noteNumbers.GetValueOrDefault(row.VoucherId, row.VoucherId.ToString())
            : issueNumbers.GetValueOrDefault(row.VoucherId, row.VoucherId.ToString().PadLeft(5, '0'));
    }

    private const string GetKardexByProductOpeningBalanceSql = """
        SELECT
            opening_quantity AS "OpeningQuantity",
            opening_value AS "OpeningValue"
        FROM sp_get_kardex_by_product_opening_balance({0}::bigint, {1}::bigint, {2}::date)
        """;

    private async Task<KardexByProductOpeningBalanceDto> GetKardexByProductOpeningBalance(
        long productId, long warehouseId, DateOnly? dateFrom)
    {
        var rows = await _unitOfWork.SqlQueryAsync<KardexByProductOpeningBalanceDto>(
            GetKardexByProductOpeningBalanceSql,
            productId,
            warehouseId,
            (object?)dateFrom ?? DBNull.Value);
        return rows.FirstOrDefault() ?? new KardexByProductOpeningBalanceDto();
    }

    public async Task<byte[]> GenerateKardexByProductReportPdf(KardexByProductReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var sections = await GetKardexByProductSections(filter);
        var allProducts = !filter.ProductId.HasValue;
        var productLabel = allProducts ? "Todos los productos" : sections.FirstOrDefault()?.ProductLabel ?? "—";
        var voucherLabel = await KardexVoucherLabels(sections.SelectMany(s => s.Rows));

        var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(filter.WarehouseId);
        var warehouseName = warehouse?.Name ?? "—";

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(8));

                page.Header().Element(c => ReportHeader(c, "Kardex por Material", $"Producto: {productLabel}", $"Almacén: {warehouseName}", $"Periodo: {periodText}"));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(62);
                        columns.ConstantColumn(62);
                        columns.ConstantColumn(68);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(62);
                        columns.ConstantColumn(68);
                        columns.ConstantColumn(70);
                        columns.ConstantColumn(62);
                        columns.ConstantColumn(68);
                        columns.ConstantColumn(70);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("");
                        header.Cell().Element(HeaderCell).Text("");
                        header.Cell().ColumnSpan(3).Element(HeaderCell).AlignCenter().Text("INGRESOS");
                        header.Cell().ColumnSpan(3).Element(HeaderCell).AlignCenter().Text("SALIDAS");
                        header.Cell().ColumnSpan(3).Element(HeaderCell).AlignCenter().Text("SALDOS");

                        header.Cell().Element(HeaderCell).Text("Fecha");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("N°\nComprobante");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    });

                    if (sections.Count == 0)
                    {
                        table.Cell().ColumnSpan(11).Element(BodyCell).Text("Sin movimientos ni saldo en este almacén y periodo.");
                    }

                    foreach (var (sectionLabel, opening, rows) in sections)
                    {
                        // Con "Todos los productos", cada producto abre su bloque con su nombre.
                        if (allProducts)
                        {
                            table.Cell().ColumnSpan(11).Element(GroupTitleCell).Text(sectionLabel).Bold().FontSize(9);
                        }

                        var openingUnitValue = opening.OpeningQuantity == 0 ? 0 : opening.OpeningValue / opening.OpeningQuantity;

                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("Saldo Inicial").Bold();
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).Text("");
                        table.Cell().Element(BodyCell).AlignRight().Text(Qty(opening.OpeningQuantity));
                        table.Cell().Element(BodyCell).AlignRight().Text(openingUnitValue.ToString("N2", ReportCulture));
                        table.Cell().Element(BodyCell).AlignRight().Text(opening.OpeningValue.ToString("N2", ReportCulture));

                        foreach (var row in rows)
                        {
                            var isReceipt = row.MovementType == "receipt";
                            var balanceUnitValue = row.BalanceQuantity == 0 ? 0 : row.BalanceValue / row.BalanceQuantity;

                            table.Cell().Element(BodyCell).Text(row.VoucherDate.ToString("dd/MM/yyyy"));
                            table.Cell().Element(BodyCell).Text(voucherLabel(row));
                            table.Cell().Element(BodyCell).AlignRight().Text(isReceipt ? Qty(row.Quantity) : "");
                            table.Cell().Element(BodyCell).AlignRight().Text(isReceipt ? row.UnitCost.ToString("N2", ReportCulture) : "");
                            table.Cell().Element(BodyCell).AlignRight().Text(isReceipt ? row.TotalCost.ToString("N2", ReportCulture) : "");
                            table.Cell().Element(BodyCell).AlignRight().Text(!isReceipt ? Qty(row.Quantity) : "");
                            table.Cell().Element(BodyCell).AlignRight().Text(!isReceipt ? row.UnitCost.ToString("N2", ReportCulture) : "");
                            table.Cell().Element(BodyCell).AlignRight().Text(!isReceipt ? row.TotalCost.ToString("N2", ReportCulture) : "");
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.BalanceQuantity));
                            table.Cell().Element(BodyCell).AlignRight().Text(balanceUnitValue.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.BalanceValue.ToString("N2", ReportCulture));
                        }
                    }
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Kardex por Material").GeneratePdf();
    }

    private const string GetPastorFieldReceivablesSql = """
        SELECT
            account_receivable_id AS "AccountReceivableId",
            issue_id AS "IssueId",
            issue_date AS "IssueDate",
            due_date AS "DueDate",
            outstanding_balance AS "OutstandingBalance",
            status AS "Status"
        FROM sp_get_pastor_field_receivables({0}::bigint)
        """;

    private const string GetPastorFieldLastDeliverySql = """
        SELECT last_delivery_date AS "LastDeliveryDate"
        FROM sp_get_pastor_field_last_delivery({0}::bigint)
        """;

    private const string GetPastorFieldDeliveriesSql = """
        SELECT
            issue_id AS "IssueId",
            issue_date AS "IssueDate",
            total AS "Total"
        FROM sp_get_pastor_field_deliveries({0}::bigint, {1}::date, {2}::date)
        """;

    private const string GetPastorFieldDepositsSql = """
        SELECT
            payment_id AS "PaymentId",
            payment_date AS "PaymentDate",
            issue_id AS "IssueId",
            amount AS "Amount",
            payment_method AS "PaymentMethod",
            notes AS "Notes"
        FROM sp_get_pastor_field_deposits({0}::bigint, {1}::date, {2}::date)
        """;

    public async Task<PastorFieldReportDataDto> GetPastorFieldReportData(PastorFieldReportQueryFilter filter)
    {
        await LoadIssueNumbersAsync();
        var receivables = await _unitOfWork.SqlQueryAsync<PastorFieldReceivableRowDto>(
            GetPastorFieldReceivablesSql, filter.ClientId);

        var lastDeliveryRows = await _unitOfWork.SqlQueryAsync<PastorFieldLastDeliveryDto>(
            GetPastorFieldLastDeliverySql, filter.ClientId);

        var deliveries = await _unitOfWork.SqlQueryAsync<PastorFieldDeliveryRowDto>(
            GetPastorFieldDeliveriesSql,
            filter.ClientId,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);

        var deposits = await _unitOfWork.SqlQueryAsync<PastorFieldDepositRowDto>(
            GetPastorFieldDepositsSql,
            filter.ClientId,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);

        // El SP devuelve el estado guardado; aquí se pasa al efectivo para mostrar "Pago retrasado".
        foreach (var receivable in receivables)
        {
            receivable.Status = AccountReceivableStatus.Effective(receivable.Status, receivable.DueDate);
        }

        var pending = receivables.Where(r => r.Status != "paid").ToList();

        return new PastorFieldReportDataDto
        {
            Receivables = receivables,
            TotalOutstanding = pending.Sum(r => r.OutstandingBalance),
            PendingCount = pending.Count,
            LastDeliveryDate = lastDeliveryRows.FirstOrDefault()?.LastDeliveryDate,
            Deliveries = deliveries,
            Deposits = deposits
        };
    }

    public async Task<byte[]> GeneratePastorFieldReportPdf(PastorFieldReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var data = await GetPastorFieldReportData(filter);

        var clientName = "—";
        var client = await _unitOfWork.Repository<Client>().GetByIdAsync(filter.ClientId);
        if (client is not null)
        {
            var party = await _unitOfWork.Repository<Party>().GetByIdAsync(client.PartyId);
            clientName = party?.Name ?? clientName;
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(9));

                page.Header().Element(c => ReportHeader(c, "Reporte Campo Pastor", $"Pastor / Cliente: {clientName}"));

                page.Content().PaddingTop(10).Column(mainColumn =>
                {
                    mainColumn.Spacing(16);

                    mainColumn.Item().Column(section =>
                    {
                        section.Item().Element(c => SectionTitle(c, "Cuentas por Cobrar"));

                        section.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.3f);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("N°");
                                header.Cell().Element(HeaderCell).Text("Fecha");
                                header.Cell().Element(HeaderCell).Text("Fecha Límite");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Saldo Pendiente");
                                header.Cell().Element(HeaderCell).Text("Estado");
                            });

                            foreach (var row in data.Receivables)
                            {
                                table.Cell().Element(BodyCell).Text(IssueNo(row.IssueId));
                                table.Cell().Element(BodyCell).Text(row.IssueDate.HasValue ? row.IssueDate.Value.ToString("dd/MM/yyyy") : "—");
                                table.Cell().Element(BodyCell).Text(row.DueDate.HasValue ? row.DueDate.Value.ToString("dd/MM/yyyy") : "—");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.OutstandingBalance.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).Text(AccountReceivableStatusLabel(row.Status));
                            }
                        });

                        section.Item().PaddingTop(8).Row(row =>
                        {
                            row.Spacing(10);
                            row.RelativeItem().Element(c => SummaryTextCard(c, "Total Pendiente", data.TotalOutstanding.ToString("N2", ReportCulture)));
                            row.RelativeItem().Element(c => SummaryTextCard(c, "Cuentas Pendientes", data.PendingCount.ToString()));
                        });
                    });

                    mainColumn.Item().Column(section =>
                    {
                        section.Item().Text(
                            $"Fecha de última entrega: {(data.LastDeliveryDate.HasValue ? data.LastDeliveryDate.Value.ToString("dd/MM/yyyy") : "—")}"
                        ).Bold().FontSize(11);
                        section.Item().PaddingTop(2).Text($"Historial del período: {periodText}").FontSize(9).Italic();
                    });

                    mainColumn.Item().Column(section =>
                    {
                        section.Item().Element(c => SectionTitle(c, "Entregas"));

                        section.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("N°");
                                header.Cell().Element(HeaderCell).Text("Fecha");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                            });

                            foreach (var row in data.Deliveries)
                            {
                                table.Cell().Element(BodyCell).Text(IssueNo(row.IssueId));
                                table.Cell().Element(BodyCell).Text(row.IssueDate.ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Total.ToString("N2", ReportCulture));
                            }
                        });
                    });

                    mainColumn.Item().Column(section =>
                    {
                        section.Item().Element(c => SectionTitle(c, "Depósitos"));

                        section.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1.2f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCell).Text("Fecha");
                                header.Cell().Element(HeaderCell).Text("N°");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Monto");
                                header.Cell().Element(HeaderCell).Text("Método");
                                header.Cell().Element(HeaderCell).Text("Notas");
                            });

                            foreach (var row in data.Deposits)
                            {
                                table.Cell().Element(BodyCell).Text(BoliviaTime.FromUtc(row.PaymentDate).ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).Text(IssueNo(row.IssueId));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Amount.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).Text(PaymentMethodLabel(row.PaymentMethod));
                                table.Cell().Element(BodyCell).Text(row.Notes ?? "");
                            }
                        });
                    });
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Reporte Campo Pastor").GeneratePdf();
    }

    private const string GetAccountReceivablesReportSql = """
        SELECT
            account_receivable_id AS "AccountReceivableId",
            issue_id AS "IssueId",
            issue_date AS "IssueDate",
            client_id AS "ClientId",
            client_name AS "ClientName",
            product_count AS "ProductCount",
            product_names AS "ProductNames",
            total_amount AS "TotalAmount",
            outstanding_balance AS "OutstandingBalance",
            paid_amount AS "PaidAmount",
            due_date AS "DueDate",
            status AS "Status"
        FROM sp_get_account_receivables_report({0}::bigint, {1}::varchar, {2}::date, {3}::date)
        """;

    // "open" (Por cobrar) no es un estado del SP: son las pendientes más las retrasadas, lo que
    // todavía falta cobrar. Es el filtro por defecto del reporte, para que con muchos pastores
    // no se genere al entrar el historial completo de cuentas ya pagadas.
    public async Task<List<AccountReceivablesReportRowDto>> GetAccountReceivablesReport(AccountReceivablesReportQueryFilter filter)
    {
        await LoadIssueNumbersAsync();
        var onlyOpen = filter.Status == "open";
        var rows = await _unitOfWork.SqlQueryAsync<AccountReceivablesReportRowDto>(
            GetAccountReceivablesReportSql,
            (object?)filter.ClientId ?? DBNull.Value,
            onlyOpen ? DBNull.Value : (object?)filter.Status ?? DBNull.Value,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);

        return onlyOpen ? rows.Where(r => r.Status != "paid").ToList() : rows;
    }

    // Una fila por producto de cada salida en cartera, con lo que Caja registró para ese producto
    // (payment_detail). Mismo formato que Salidas de Almacén, pero agrupado por cliente:
    // la categoría del producto no le sirve a quien cobra. Cada fila tiene sus propios montos,
    // así los subtotales suman bien.
    private sealed record ReceivableProductRow(
        AccountReceivablesReportRowDto Account,
        string? Code,
        string Name,
        string? UnitMeasure,
        string Department,
        string SubDepartment,
        decimal Quantity,
        decimal UnitCost,
        decimal TotalCost,
        decimal Paid)
    {
        public decimal Balance => Math.Max(TotalCost - Paid, 0);
    }

    private async Task<List<ReceivableProductRow>> GetReceivableProductRowsAsync(List<AccountReceivablesReportRowDto> accounts)
    {
        var issueIds = accounts.Where(a => a.IssueId.HasValue).Select(a => a.IssueId!.Value).Distinct().ToList();

        var lines = await (
                from d in _unitOfWork.Repository<IssueDetail>().Query()
                join p in _unitOfWork.Repository<Product>().Query() on d.ProductId equals p.ProductId
                join dep in _unitOfWork.Repository<Department>().Query() on p.DepartmentId equals (long?)dep.DepartmentId into deps
                from dep in deps.DefaultIfEmpty()
                join sub in _unitOfWork.Repository<SubDepartment>().Query() on p.SubDepartmentId equals (long?)sub.SubDepartmentId into subs
                from sub in subs.DefaultIfEmpty()
                join mt in _unitOfWork.Repository<MediaType>().Query() on p.MediaTypeId equals (long?)mt.MediaTypeId into mts
                from mt in mts.DefaultIfEmpty()
                where issueIds.Contains(d.IssueId)
                orderby d.IssueDetailId
                select new
                {
                    d.IssueDetailId,
                    d.IssueId,
                    p.Code,
                    p.Name,
                    UnitMeasure = mt == null ? null : mt.Name,
                    Department = dep == null ? null : dep.Name,
                    SubDepartment = sub == null ? null : sub.Name,
                    d.Quantity,
                    d.UnitCost,
                    d.TotalCost
                })
            .ToListAsync();

        var issueDetailIds = lines.Select(l => l.IssueDetailId).ToList();
        var paidByLine = await _unitOfWork.Repository<PaymentDetail>().Query()
            .Where(pd => issueDetailIds.Contains(pd.IssueDetailId))
            .GroupBy(pd => pd.IssueDetailId)
            .Select(g => new { IssueDetailId = g.Key, Paid = g.Sum(pd => pd.Amount) })
            .ToDictionaryAsync(x => x.IssueDetailId, x => x.Paid);

        var linesByIssue = lines.GroupBy(l => l.IssueId).ToDictionary(g => g.Key, g => g.ToList());

        var result = new List<ReceivableProductRow>();
        foreach (var account in accounts)
        {
            if (account.IssueId.HasValue && linesByIssue.TryGetValue(account.IssueId.Value, out var issueLines))
            {
                result.AddRange(issueLines.Select(l => new ReceivableProductRow(
                    account, l.Code, l.Name, l.UnitMeasure,
                    l.Department ?? "(Sin categoría)", l.SubDepartment ?? "(Sin subcategoría)",
                    l.Quantity, l.UnitCost, l.TotalCost, paidByLine.GetValueOrDefault(l.IssueDetailId))));
            }
            else
            {
                // Cuenta creada a mano, sin salida: una sola fila con el monto de la cuenta.
                result.Add(new ReceivableProductRow(
                    account, null, account.ProductNames ?? "(Cuenta sin productos)", null,
                    "(Sin categoría)", "(Sin subcategoría)",
                    0, 0, account.TotalAmount, account.TotalAmount - account.OutstandingBalance));
            }
        }

        return result;
    }

    public async Task<byte[]> GenerateAccountReceivablesReportPdf(AccountReceivablesReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var accounts = await GetAccountReceivablesReport(filter);
        var productRows = await GetReceivableProductRowsAsync(accounts);

        var clientName = "Todos los clientes";
        if (filter.ClientId.HasValue)
        {
            var client = await _unitOfWork.Repository<Client>().GetByIdAsync(filter.ClientId.Value);
            if (client is not null)
            {
                var party = await _unitOfWork.Repository<Party>().GetByIdAsync(client.PartyId);
                clientName = party?.Name ?? clientName;
            }
        }

        var statusLabel = filter.Status switch
        {
            "open" => "Por cobrar (pendientes y con pago retrasado)",
            "pending" => "Pendientes",
            "late" => "Pago retrasado",
            "paid" => "Pagadas",
            _ => "Todos los estados"
        };

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var periodText = (filter.DateFrom, filter.DateTo) switch
        {
            (not null, not null) => $"{filter.DateFrom:dd/MM/yyyy} al {filter.DateTo:dd/MM/yyyy}",
            (not null, null) => $"Desde {filter.DateFrom:dd/MM/yyyy}",
            (null, not null) => $"Hasta {filter.DateTo:dd/MM/yyyy}",
            _ => "Todo el historial"
        };

        var clients = productRows
            .GroupBy(r => r.Account.ClientName ?? "(Sin cliente)")
            .OrderBy(g => g.Key)
            .ToList();

        const int leadColumns = 6;
        const int totalColumns = leadColumns + 6;

        var pendingAccounts = accounts.Count(a => a.Status != "paid");
        var grandTotal = productRows.Sum(r => r.TotalCost);
        var grandPaid = productRows.Sum(r => r.Paid);
        var grandBalance = productRows.Sum(r => r.Balance);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(24);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(6.5f));

                page.Header().Element(c => ReportHeader(c, "Reporte General de Cuentas por Cobrar", $"Cliente: {clientName}", $"Estado: {statusLabel}", $"Fecha límite: {periodText}"));

                page.Content().PaddingTop(10).Column(mainColumn =>
                {
                    mainColumn.Spacing(10);

                    mainColumn.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(32);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(32);
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(36);
                            columns.ConstantColumn(36);
                            columns.ConstantColumn(40);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(44);
                            columns.ConstantColumn(44);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("N°");
                            header.Cell().Element(HeaderCell).Text("Fecha");
                            header.Cell().Element(HeaderCell).Text("Fecha\nLímite");
                            header.Cell().Element(HeaderCell).Text("Código");
                            header.Cell().Element(HeaderCell).Text("Descripción");
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nTotal");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Pagado");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Saldo");
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Estado");
                        });

                        void TotalsRow(Func<IContainer, IContainer> style, string label, decimal quantity, decimal total, decimal paid, decimal balance)
                        {
                            table.Cell().ColumnSpan(leadColumns).Element(style).AlignRight().Text(label).Bold();
                            table.Cell().Element(style).AlignRight().Text(Qty(quantity)).Bold();
                            table.Cell().Element(style).Text("");
                            table.Cell().Element(style).AlignRight().Text(total.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(style).AlignRight().Text(paid.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(style).AlignRight().Text(balance.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(style).Text("");
                        }

                        foreach (var client in clients)
                        {
                            table.Cell().ColumnSpan(totalColumns).Element(GroupTitleCell)
                                .Text(client.Key).Bold().FontSize(10);

                            decimal qty = 0m, total = 0m, paid = 0m, balance = 0m;

                            foreach (var row in client.OrderBy(r => r.Account.IssueId).ThenBy(r => r.Name))
                            {
                                var account = row.Account;
                                table.Cell().Element(BodyCell).Text(IssueNo(account.IssueId));
                                table.Cell().Element(BodyCell).Text(account.IssueDate.HasValue ? account.IssueDate.Value.ToString("dd/MM/yyyy") : "—");
                                table.Cell().Element(BodyCell).Text(account.DueDate.HasValue ? account.DueDate.Value.ToString("dd/MM/yyyy") : "—");
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.Quantity));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.UnitCost.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalCost.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Paid.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Balance.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignCenter().Text(AccountReceivableStatusLabel(account.Status));

                                qty += row.Quantity;
                                total += row.TotalCost;
                                paid += row.Paid;
                                balance += row.Balance;
                            }

                            TotalsRow(SubtotalCell, "Total cliente", qty, total, paid, balance);
                        }

                        if (clients.Count > 0)
                        {
                            TotalsRow(CategoryTotalCell, "TOTAL GENERAL", productRows.Sum(r => r.Quantity), grandTotal, grandPaid, grandBalance);
                        }
                    });

                    mainColumn.Item().PaddingTop(8).Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Total Cartera", grandTotal.ToString("N2", ReportCulture)));
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Total Pagado", grandPaid.ToString("N2", ReportCulture)));
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Total Pendiente", grandBalance.ToString("N2", ReportCulture)));
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Cuentas Pendientes", pendingAccounts.ToString()));
                    });
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Cartera de Cuentas por Cobrar").GeneratePdf();
    }

    // Comprobante de una sola Cuenta por Cobrar (a diferencia del reporte general de arriba,
    // que lista varias). La Cuenta por Cobrar no tiene líneas propias: reutiliza las de la
    // Salida vinculada (mismo query que el comprobante de Salida) para mostrar código y
    // nombre de cada producto que se está cobrando, y agrega el historial de pagos.
    public async Task<byte[]?> GenerateAccountReceivableVoucherPdf(long accountReceivableId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var accountReceivable = await _unitOfWork.Repository<AccountReceivable>().GetByIdAsync(accountReceivableId);
        if (accountReceivable is null)
        {
            return null;
        }

        var lines = accountReceivable.IssueId.HasValue
            ? await _unitOfWork.SqlQueryAsync<IssueVoucherLineDto>(GetIssueVoucherLinesSql, accountReceivable.IssueId.Value)
            : [];

        Issue? issue = accountReceivable.IssueId.HasValue
            ? await _unitOfWork.Repository<Issue>().GetByIdAsync(accountReceivable.IssueId.Value)
            : null;

        var clientName = "—";
        if (accountReceivable.ClientId.HasValue)
        {
            var client = await _unitOfWork.Repository<Client>().GetByIdAsync(accountReceivable.ClientId.Value);
            if (client is not null)
            {
                var party = await _unitOfWork.Repository<Party>().GetByIdAsync(client.PartyId);
                clientName = party?.Name ?? clientName;
            }
        }

        var payments = await _unitOfWork.Repository<Payment>().Query()
            .Where(p => p.AccountReceivableId == accountReceivableId)
            .OrderBy(p => p.PaymentDate)
            .ToListAsync();

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var productsTotal = lines.Sum(l => l.TotalCost);

        var paymentTypeLabel = accountReceivable.PaymentType switch
        {
            "cash" => "Al contado",
            "credit" => "Crédito",
            "installments" => "A cuotas",
            _ => accountReceivable.PaymentType
        };

        var statusLabel = AccountReceivableStatusLabel(EffectiveAccountReceivableStatus(accountReceivable.Status, accountReceivable.DueDate));

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(9));

                page.Header().Element(c => ReportHeader(c, $"Cuenta por Cobrar N° {accountReceivableId.ToString().PadLeft(5, '0')}",
                    $"Cliente: {clientName}",
                    $"N° salida: {(issue is not null ? IssueNumbering.Format(issue.MonthlyNumber, issue.IssueDate, issue.IssueId) : "—")}",
                    $"Fecha: {(issue is not null ? issue.IssueDate.ToString("dd/MM/yyyy") : "—")}",
                    $"Forma de pago: {paymentTypeLabel}",
                    $"Fecha límite: {(accountReceivable.DueDate.HasValue ? accountReceivable.DueDate.Value.ToString("dd/MM/yyyy") : "—")}",
                    $"Estado: {statusLabel}"));

                page.Content().PaddingTop(10).Column(mainColumn =>
                {
                    mainColumn.Item().Element(c => SectionTitle(c, "Productos"));

                    mainColumn.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(42);
                            columns.ConstantColumn(80);
                            columns.RelativeColumn(1);
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(48);
                            columns.ConstantColumn(68);
                            columns.ConstantColumn(68);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("CÓDIGO");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("DEPT.");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("DESCRIPCIÓN");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("UNIDAD\nDE MEDIDA");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("CANTIDAD");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("COSTO\nUNITARIO (Bs.)");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("COSTO\nTOTAL (Bs.)");
                        });

                        if (lines.Count == 0)
                        {
                            table.Cell().ColumnSpan(7).Element(VoucherBodyCell).AlignCenter().Text("Sin productos asociados.");
                        }
                        else
                        {
                            foreach (var line in lines)
                            {
                                table.Cell().Element(VoucherBodyCell).Text(line.Code ?? "—");
                                table.Cell().Element(VoucherBodyCell).Text(line.DepartmentName ?? "—");
                                table.Cell().Element(VoucherBodyCell).Text(line.Name);
                                table.Cell().Element(VoucherBodyCell).AlignCenter().Text(line.UnitMeasure ?? "—");
                                table.Cell().Element(VoucherBodyCell).AlignRight().Text(Qty(line.Quantity));
                                table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.UnitCost.ToString("N2", ReportCulture));
                                table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.TotalCost.ToString("N2", ReportCulture));
                            }

                            table.Cell().ColumnSpan(6).Element(VoucherTotalCell).AlignRight().Text("TOTAL PRODUCTOS (Bs.)").Bold();
                            table.Cell().Element(VoucherTotalCell).AlignRight().Text(productsTotal.ToString("N2", ReportCulture)).Bold();
                        }
                    });

                    mainColumn.Item().PaddingTop(14).Element(c => SectionTitle(c, "Pagos"));

                    mainColumn.Item().PaddingTop(4).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("FECHA");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("MÉTODO");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("NOTAS");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("MONTO\n(Bs.)");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("SALDO\n(Bs.)");
                        });

                        if (payments.Count == 0)
                        {
                            table.Cell().ColumnSpan(5).Element(VoucherBodyCell).AlignCenter().Text("Sin pagos registrados.");
                        }
                        else
                        {
                            var balance = accountReceivable.TotalAmount;
                            foreach (var payment in payments)
                            {
                                balance = Math.Max(balance - payment.Amount, 0);
                                table.Cell().Element(VoucherBodyCell).Text(BoliviaTime.FromUtc(payment.PaymentDate).ToString("dd/MM/yyyy HH:mm"));
                                table.Cell().Element(VoucherBodyCell).Text(PaymentMethodLabel(payment.PaymentMethod));
                                table.Cell().Element(VoucherBodyCell).Text(payment.Notes ?? "—");
                                table.Cell().Element(VoucherBodyCell).AlignRight().Text(payment.Amount.ToString("N2", ReportCulture));
                                table.Cell().Element(VoucherBodyCell).AlignRight().Text(balance.ToString("N2", ReportCulture));
                            }
                        }
                    });

                    mainColumn.Item().PaddingTop(10).Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Total", accountReceivable.TotalAmount.ToString("N2", ReportCulture)));
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Saldo Pendiente", accountReceivable.OutstandingBalance.ToString("N2", ReportCulture)));
                    });
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, $"Cuenta por Cobrar N° {accountReceivableId.ToString().PadLeft(5, '0')}").GeneratePdf();
    }

    private static byte[] GetLogoBytes()
    {
        if (_logoBytes is not null)
        {
            return _logoBytes;
        }

        using var stream = typeof(ReportService).Assembly
            .GetManifestResourceStream("CheckInvoice.Application.Resources.mbos-logo.png")
            ?? throw new InvalidOperationException("Logo embebido (Resources/mbos-logo.png) no encontrado.");

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        _logoBytes = memoryStream.ToArray();
        return _logoBytes;
    }

    // Título del PDF: lo muestran el visor del navegador y la pestaña en lugar del nombre interno
    // del archivo temporal (un código como "97402a5b-178a-..."), que no le dice nada al usuario.
    private static Document WithTitle(Document document, string title) =>
        document.WithMetadata(new DocumentMetadata { Title = title, Author = "InvenTrack" });

    private async Task<FinancialDashboardSummaryDto> GetSummaryAsync()
    {
        var now = BoliviaTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var totalIssues = await (
            from d in _unitOfWork.Repository<IssueDetail>().Query()
            join i in _unitOfWork.Repository<Issue>().Query() on d.IssueId equals i.IssueId
            where !i.IsVoided && i.IssueDate >= monthStart && i.IssueDate < monthEnd
            select d.TotalCost
        ).SumAsync(x => (decimal?)x) ?? 0;

        var totalReceipts = await (
            from d in _unitOfWork.Repository<ReceiptDetail>().Query()
            join r in _unitOfWork.Repository<Receipt>().Query() on d.ReceiptId equals r.ReceiptId
            where !r.IsVoided && r.IssueDate >= monthStart && r.IssueDate < monthEnd
            select d.TotalCost
        ).SumAsync(x => (decimal?)x) ?? 0;

        var totalPending = await _unitOfWork.Repository<AccountReceivable>().Query()
            .Where(a => a.Status == "pending")
            .SumAsync(a => (decimal?)a.OutstandingBalance) ?? 0;

        var totalPayments = await _unitOfWork.Repository<Payment>().Query()
            .Where(p => p.PaymentDate >= monthStart && p.PaymentDate < monthEnd)
            .SumAsync(p => (decimal?)p.Amount) ?? 0;

        return new FinancialDashboardSummaryDto
        {
            TotalIssuesThisMonth = totalIssues,
            TotalReceiptsThisMonth = totalReceipts,
            TotalPendingReceivable = totalPending,
            TotalPaymentsThisMonth = totalPayments
        };
    }

    private async Task<List<MonthlyTotalDto>> GetIssuesByMonthAsync()
    {
        var now = BoliviaTime.Now;
        var rangeStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(MonthsBack - 1));

        var raw = await (
            from d in _unitOfWork.Repository<IssueDetail>().Query()
            join i in _unitOfWork.Repository<Issue>().Query() on d.IssueId equals i.IssueId
            where !i.IsVoided && i.IssueDate >= rangeStart
            select new { i.IssueDate, d.TotalCost }
        )
            .GroupBy(x => new { x.IssueDate.Year, x.IssueDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(x => x.TotalCost) })
            .ToListAsync();

        var result = new List<MonthlyTotalDto>();
        for (var i = 0; i < MonthsBack; i++)
        {
            var month = rangeStart.AddMonths(i);
            var match = raw.FirstOrDefault(r => r.Year == month.Year && r.Month == month.Month);
            result.Add(new MonthlyTotalDto
            {
                MonthLabel = month.ToString("MMM yyyy", ReportCulture),
                Total = match?.Total ?? 0
            });
        }

        return result;
    }

    private async Task<ReceivableStatusSummaryDto> GetReceivableStatusSummaryAsync()
    {
        var pending = await _unitOfWork.Repository<AccountReceivable>().Query()
            .Where(a => a.Status == "pending")
            .SumAsync(a => (decimal?)a.OutstandingBalance) ?? 0;

        var paid = await _unitOfWork.Repository<AccountReceivable>().Query()
            .Where(a => a.Status == "paid")
            .SumAsync(a => (decimal?)a.TotalAmount) ?? 0;

        return new ReceivableStatusSummaryDto { PendingTotal = pending, PaidTotal = paid };
    }

}
