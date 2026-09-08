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
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Reports;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

public class ReportService : IReportService
{
    private const int MonthsBack = 6;
    private static readonly CultureInfo ReportCulture = new("es-ES");
    private static byte[]? _logoBytes;

    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUserService;

    public ReportService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _currentUserService = currentUserService;
    }

    public async Task<byte[]> GenerateFinancialDashboardPdf()
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var summary = await GetSummaryAsync();
        var issuesByMonth = await GetIssuesByMonthAsync();
        var receivableStatus = await GetReceivableStatusSummaryAsync();

        var issuesChart = BuildIssuesByMonthChart(issuesByMonth);
        var receivableChart = BuildReceivableStatusChart(receivableStatus);

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
                page.DefaultTextStyle(x => x.FontSize(11));

                page.Header().Column(column =>
                {
                    column.Item().Text("Reporte Financiero").FontSize(20).Bold();
                    column.Item().Text($"Generado el {DateTime.UtcNow:dd/MM/yyyy HH:mm}").FontSize(9).FontColor(Colors.Grey.Darken1);
                });

                page.Content().PaddingTop(15).Column(column =>
                {
                    column.Spacing(15);

                    column.Item().Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Element(c => SummaryCard(c, "Salidas del mes", summary.TotalIssuesThisMonth));
                        row.RelativeItem().Element(c => SummaryCard(c, "Entradas del mes", summary.TotalReceiptsThisMonth));
                        row.RelativeItem().Element(c => SummaryCard(c, "Pendiente por cobrar", summary.TotalPendingReceivable));
                        row.RelativeItem().Element(c => SummaryCard(c, "Pagos del mes", summary.TotalPaymentsThisMonth));
                    });

                    column.Item().Text($"Salidas por mes (últimos {MonthsBack} meses)").FontSize(13).Bold();
                    column.Item().Height(220).Image(issuesChart);

                    column.Item().Text("Cuentas por Cobrar: pendiente vs pagado").FontSize(13).Bold();
                    column.Item().Height(220).Image(receivableChart);
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("INVENTARIO GENERAL").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Almacén: {warehouseName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Periodo: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

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
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nTotal");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(6).Element(c => c.PaddingTop(8).PaddingBottom(2))
                            .Text(dept.Department).Bold().FontSize(10);

                        var deptTotal = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(6).Element(c => c.PaddingBottom(2))
                                .Text(sub.Key).FontSize(9);

                            var subTotal = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name))
                            {
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.NetQuantity.ToString("N2", ReportCulture));
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

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("PLANTILLA DE CONTEO FÍSICO").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Almacén: {warehouseName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Periodo: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

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
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nTotal");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad\nFísica");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Diferencia");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(8).Element(c => c.PaddingTop(8).PaddingBottom(2))
                            .Text(dept.Department).Bold().FontSize(10);

                        var deptQty = 0m;
                        var deptTotalValue = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(8).Element(c => c.PaddingBottom(2))
                                .Text(sub.Key).FontSize(9);

                            var subQty = 0m;
                            var subTotalValue = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name))
                            {
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.NetQuantity.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.UnitValue.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalValue.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).Text("");
                                table.Cell().Element(BodyCell).Text("");

                                subQty += row.NetQuantity;
                                subTotalValue += row.TotalValue;
                            }

                            table.Cell().ColumnSpan(3).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subQty.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotalValue.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).Text("");

                            deptQty += subQty;
                            deptTotalValue += subTotalValue;
                        }

                        table.Cell().ColumnSpan(3).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptQty.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotalValue.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).Text("");
                    }
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("INVENTARIO POR DEPARTAMENTO").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Almacén: {warehouseName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Categoría: {departmentName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Periodo: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

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
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nTotal");
                    });

                    var grandTotal = 0m;

                    foreach (var sub in groups)
                    {
                        table.Cell().ColumnSpan(6).Element(c => c.PaddingTop(8).PaddingBottom(2))
                            .Text(sub.Key).Bold().FontSize(10);

                        var subTotal = 0m;

                        foreach (var row in sub.OrderBy(r => r.Name))
                        {
                            table.Cell().Element(BodyCell).Text(row.Code ?? "");
                            table.Cell().Element(BodyCell).Text(row.Name);
                            table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                            table.Cell().Element(BodyCell).AlignRight().Text(row.NetQuantity.ToString("N2", ReportCulture));
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

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("KARDEX FÍSICO VALORADO").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Almacén: {warehouseName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Periodo: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

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
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldo Inicial\nValor");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Entradas\nCant.");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Entradas\nValor");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Salidas\nCant.");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Salidas\nValor");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldos\nCant.");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Saldos\nValor");
                    });

                    decimal grandReceiptsQty = 0m, grandReceiptsValue = 0m;
                    decimal grandIssuesQty = 0m, grandIssuesValue = 0m;
                    decimal grandClosingQty = 0m, grandClosingValue = 0m;

                    foreach (var sub in groups)
                    {
                        table.Cell().ColumnSpan(10).Element(c => c.PaddingTop(8).PaddingBottom(2))
                            .Text(sub.Key).Bold().FontSize(10);

                        decimal subReceiptsQty = 0m, subReceiptsValue = 0m;
                        decimal subIssuesQty = 0m, subIssuesValue = 0m;
                        decimal subClosingQty = 0m, subClosingValue = 0m;

                        foreach (var row in sub.OrderBy(r => r.Name))
                        {
                            table.Cell().Element(BodyCell).Text(row.Code ?? "");
                            table.Cell().Element(BodyCell).Text(row.Name);
                            table.Cell().Element(BodyCell).AlignRight().Text(row.OpeningQuantity.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.OpeningValue.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.ReceiptsQuantity.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.ReceiptsValue.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.IssuesQuantity.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.IssuesValue.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.ClosingQuantity.ToString("N2", ReportCulture));
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
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subReceiptsQty.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subReceiptsValue.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subIssuesQty.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subIssuesValue.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(subClosingQty.ToString("N2", ReportCulture)).Bold();
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
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandReceiptsQty.ToString("N2", ReportCulture)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandReceiptsValue.ToString("N2", ReportCulture)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandIssuesQty.ToString("N2", ReportCulture)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandIssuesValue.ToString("N2", ReportCulture)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandClosingQty.ToString("N2", ReportCulture)).Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandClosingValue.ToString("N2", ReportCulture)).Bold();
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("LEVANTAMIENTO DE INVENTARIO").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Almacén: {warehouseName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Periodo: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

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
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nTotal");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad\nFísica");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Diferencia");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(8).Element(c => c.PaddingTop(8).PaddingBottom(2))
                            .Text(dept.Department).Bold().FontSize(10);

                        decimal deptQty = 0m, deptTotalValue = 0m, deptPhysicalQty = 0m, deptDifference = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(8).Element(c => c.PaddingBottom(2))
                                .Text(sub.Key).FontSize(9);

                            decimal subQty = 0m, subTotalValue = 0m, subPhysicalQty = 0m, subDifference = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name))
                            {
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Quantity.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text((row.UnitValue ?? 0).ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalValue.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text((row.PhysicalQuantity ?? 0).ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text((row.Difference ?? 0).ToString("N2", ReportCulture));

                                subQty += row.Quantity;
                                subTotalValue += row.TotalValue;
                                subPhysicalQty += row.PhysicalQuantity ?? 0;
                                subDifference += row.Difference ?? 0;
                            }

                            table.Cell().ColumnSpan(3).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subQty.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotalValue.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subPhysicalQty.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subDifference.ToString("N2", ReportCulture)).Bold();

                            deptQty += subQty;
                            deptTotalValue += subTotalValue;
                            deptPhysicalQty += subPhysicalQty;
                            deptDifference += subDifference;
                        }

                        table.Cell().ColumnSpan(3).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptQty.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotalValue.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptPhysicalQty.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptDifference.ToString("N2", ReportCulture)).Bold();
                    }
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("SALIDAS DE ALMACÉN").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Almacén: {warehouseName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Periodo: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

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
                        header.Cell().Element(HeaderCell).Text("N° Salida");
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nTotal");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(7).Element(c => c.PaddingTop(8).PaddingBottom(2))
                            .Text(dept.Department).Bold().FontSize(10);

                        decimal deptQty = 0m, deptTotalValue = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(7).Element(c => c.PaddingBottom(2))
                                .Text(sub.Key).FontSize(9);

                            decimal subQty = 0m, subTotalValue = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name).ThenBy(r => r.IssueId))
                            {
                                table.Cell().Element(BodyCell).Text(row.IssueId.ToString().PadLeft(5, '0'));
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Quantity.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.UnitCost.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalCost.ToString("N2", ReportCulture));

                                subQty += row.Quantity;
                                subTotalValue += row.TotalCost;
                            }

                            table.Cell().ColumnSpan(4).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subQty.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotalValue.ToString("N2", ReportCulture)).Bold();

                            deptQty += subQty;
                            deptTotalValue += subTotalValue;
                        }

                        table.Cell().ColumnSpan(4).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptQty.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotalValue.ToString("N2", ReportCulture)).Bold();
                    }
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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
                page.DefaultTextStyle(x => x.FontSize(7));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("INGRESOS DE ALMACÉN").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Almacén: {warehouseName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Periodo: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(38);
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
                        header.Cell().Element(HeaderCell).Text("N° Ingreso");
                        header.Cell().Element(HeaderCell).Text("Fecha");
                        header.Cell().Element(HeaderCell).Text("Código");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad de\nMedida");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nTotal");
                    });

                    foreach (var dept in groups)
                    {
                        table.Cell().ColumnSpan(8).Element(c => c.PaddingTop(8).PaddingBottom(2))
                            .Text(dept.Department).Bold().FontSize(10);

                        decimal deptQty = 0m, deptTotalValue = 0m;

                        foreach (var sub in dept.SubGroups)
                        {
                            table.Cell().ColumnSpan(8).Element(c => c.PaddingBottom(2))
                                .Text(sub.Key).FontSize(9);

                            decimal subQty = 0m, subTotalValue = 0m;

                            foreach (var row in sub.OrderBy(r => r.Name).ThenBy(r => r.ReceiptId))
                            {
                                table.Cell().Element(BodyCell).Text(row.ReceiptId.ToString().PadLeft(5, '0'));
                                table.Cell().Element(BodyCell).Text(row.ReceiptDate.ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).Text(row.Code ?? "");
                                table.Cell().Element(BodyCell).Text(row.Name);
                                table.Cell().Element(BodyCell).Text(row.UnitMeasure ?? "");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Quantity.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.UnitCost.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.TotalCost.ToString("N2", ReportCulture));

                                subQty += row.Quantity;
                                subTotalValue += row.TotalCost;
                            }

                            table.Cell().ColumnSpan(5).Element(SubtotalCell).AlignRight().Text("Total sub Categoría").Bold();
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subQty.ToString("N2", ReportCulture)).Bold();
                            table.Cell().Element(SubtotalCell).Text("");
                            table.Cell().Element(SubtotalCell).AlignRight().Text(subTotalValue.ToString("N2", ReportCulture)).Bold();

                            deptQty += subQty;
                            deptTotalValue += subTotalValue;
                        }

                        table.Cell().ColumnSpan(5).Element(CategoryTotalCell).AlignRight().Text("Total Categoría").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptQty.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(deptTotalValue.ToString("N2", ReportCulture)).Bold();
                    }
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private const string GetReceiptVoucherLinesSql = """
        SELECT
            code AS "Code",
            name AS "Name",
            unit_measure AS "UnitMeasure",
            department_name AS "DepartmentName",
            quantity AS "Quantity",
            unit_cost AS "UnitCost",
            total_cost AS "TotalCost"
        FROM sp_get_receipt_voucher_lines({0}::bigint)
        """;

    public async Task<byte[]?> GenerateReceiptVoucherPdf(long receiptId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var receipt = await _unitOfWork.Repository<Receipt>().GetByIdAsync(receiptId);
        if (receipt is null)
        {
            return null;
        }

        var lines = await _unitOfWork.SqlQueryAsync<ReceiptVoucherLineDto>(GetReceiptVoucherLinesSql, receiptId);

        var warehouseName = "—";
        if (receipt.WarehouseId.HasValue)
        {
            var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(receipt.WarehouseId.Value);
            warehouseName = warehouse?.Name ?? warehouseName;
        }

        var supplierName = "—";
        if (receipt.SupplierId.HasValue)
        {
            var supplier = await _unitOfWork.Repository<Supplier>().GetByIdAsync(receipt.SupplierId.Value);
            supplierName = supplier?.LegalName ?? supplierName;
        }

        var receiptTypeName = "—";
        if (receipt.ReceiptTypeId.HasValue)
        {
            var receiptType = await _unitOfWork.Repository<ReceiptType>().GetByIdAsync(receipt.ReceiptTypeId.Value);
            receiptTypeName = receiptType?.Name ?? receiptTypeName;
        }

        // El nombre de quien "Recibí Conforme" se preimprime con el usuario que registró la
        // entrada en el sistema — Autorizado Por / Departamento Contable quedan en blanco
        // para firma manual, igual que en el formato en papel que ya usa el cliente.
        var receivedByName = "—";
        if (receipt.CreatedById.HasValue)
        {
            var creator = await _unitOfWork.Repository<AppUser>().GetByIdAsync(receipt.CreatedById.Value);
            if (creator is not null)
            {
                receivedByName = $"{creator.FirstName} {creator.LastName}".Trim();
            }
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var total = lines.Sum(l => l.TotalCost);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.ConstantItem(65).Height(48).Image(GetLogoBytes()).FitArea();
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(10);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(10);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(10);
                        });
                        row.ConstantItem(110).Height(48).Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Column(c =>
                        {
                            c.Item().AlignCenter().Text("NRO. DE ENTRADA").FontSize(7).FontColor(Colors.Grey.Darken2);
                            c.Item().AlignCenter().Text(receiptId.ToString()).Bold().FontSize(16);
                        });
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("INGRESO A ALMACÉN").Bold().FontSize(14);
                });

                page.Content().PaddingTop(10).Column(mainColumn =>
                {
                    mainColumn.Item().PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("ALMACÉN: ").Bold();
                            t.Span(warehouseName);
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("FECHA: ").Bold();
                            t.Span(receipt.IssueDate.ToString("dd/MM/yyyy"));
                        });
                    });

                    mainColumn.Item().PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("PROVEEDOR: ").Bold();
                            t.Span(supplierName);
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("TIPO DE INGRESO: ").Bold();
                            t.Span(receiptTypeName);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(receipt.Description))
                    {
                        mainColumn.Item().PaddingBottom(6).Text(t =>
                        {
                            t.Span("DESCRIPCIÓN GENERAL: ").Bold();
                            t.Span(receipt.Description);
                        });
                    }

                    mainColumn.Item().PaddingTop(6).Table(table =>
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
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("VALOR\nUNITARIO (Bs.)");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("VALOR\nTOTAL (Bs.)");
                        });

                        foreach (var line in lines)
                        {
                            table.Cell().Element(VoucherBodyCell).Text(line.Code ?? "—");
                            table.Cell().Element(VoucherBodyCell).Text(line.DepartmentName ?? "—");
                            table.Cell().Element(VoucherBodyCell).Text(line.Name);
                            table.Cell().Element(VoucherBodyCell).AlignCenter().Text(line.UnitMeasure ?? "—");
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.Quantity.ToString("N2", ReportCulture));
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.UnitCost.ToString("N2", ReportCulture));
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.TotalCost.ToString("N2", ReportCulture));
                        }

                        table.Cell().ColumnSpan(6).Element(VoucherTotalCell).AlignRight().Text("TOTALES (Bs.)").Bold();
                        table.Cell().Element(VoucherTotalCell).AlignRight().Text(total.ToString("N2", ReportCulture)).Bold();
                    });

                    mainColumn.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(c => SignatureBox(c, "Autorizado Por:", null));
                        row.ConstantItem(20);
                        row.RelativeItem().Column(c => SignatureBox(c, "Recibí Conforme:", receivedByName));
                        row.ConstantItem(20);
                        row.RelativeItem().Column(c => SignatureBox(c, "Departamento Contable:", null));
                    });
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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

    public async Task<byte[]?> GenerateIssueVoucherPdf(long issueId, long? printTypeId = null)
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

        // "Responsable de Inventarios" se preimprime con el usuario que registró la salida en
        // el sistema — Autorizado Por queda en blanco para firma manual. "Recibí Conforme" es
        // el Cliente (quien recibe el material), a diferencia del comprobante de Entrada donde
        // ese rol lo cumple quien registró el ingreso.
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

        // "Factura" y "Recibo" se imprimen en rollo térmico angosto (como ya se hace en el
        // negocio con ventas al contado) — "Nota de Entrega" y cualquier otro tipo usan el
        // formato de página completa. Por decisión explícita del cliente, el rollo NO incluye
        // NIT/N° Autorización/Código de Control: esos son datos de facturación fiscal oficial
        // (SIN) que este sistema no gestiona, y no deben inventarse.
        var isRoll = printTypeName.Equals("Factura", StringComparison.OrdinalIgnoreCase)
            || printTypeName.Equals("Recibo", StringComparison.OrdinalIgnoreCase);

        var document = isRoll
            ? BuildIssueVoucherRollDocument(issueId, printTypeName, issue, lines, total, warehouseName, clientName, username)
            : BuildIssueVoucherFullPageDocument(issueId, printTypeName, issue, lines, total, warehouseName, clientName, inventoryManagerName, username);

        return document.GeneratePdf();
    }

    private static Document BuildIssueVoucherFullPageDocument(
        long issueId,
        string printTypeName,
        Issue issue,
        List<IssueVoucherLineDto> lines,
        decimal total,
        string warehouseName,
        string clientName,
        string inventoryManagerName,
        string username)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.ConstantItem(65).Height(48).Image(GetLogoBytes()).FitArea();
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(10);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(10);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(10);
                        });
                        row.ConstantItem(110).Height(48).Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Column(c =>
                        {
                            c.Item().AlignCenter().Text("NRO. DE SALIDA").FontSize(7).FontColor(Colors.Grey.Darken2);
                            c.Item().AlignCenter().Text(issueId.ToString()).Bold().FontSize(16);
                        });
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("SALIDA DE ALMACÉN").Bold().FontSize(14);
                });

                page.Content().PaddingTop(10).Column(mainColumn =>
                {
                    mainColumn.Item().PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("ALMACÉN: ").Bold();
                            t.Span(warehouseName);
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("FECHA: ").Bold();
                            t.Span(issue.IssueDate.ToString("dd/MM/yyyy"));
                        });
                    });

                    mainColumn.Item().PaddingBottom(6).Text(t =>
                    {
                        t.Span("TIPO DE SALIDA: ").Bold();
                        t.Span(printTypeName);
                    });

                    mainColumn.Item().PaddingBottom(6).Text(t =>
                    {
                        t.Span("DESCRIPCIÓN GENERAL: ").Bold();
                        t.Span(string.IsNullOrWhiteSpace(issue.Description) ? "-" : issue.Description);
                    });

                    mainColumn.Item().PaddingTop(6).Table(table =>
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
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("VALOR\nUNITARIO (Bs.)");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("VALOR\nTOTAL (Bs.)");
                        });

                        foreach (var line in lines)
                        {
                            table.Cell().Element(VoucherBodyCell).Text(line.Code ?? "—");
                            table.Cell().Element(VoucherBodyCell).Text(line.DepartmentName ?? "—");
                            table.Cell().Element(VoucherBodyCell).Text(line.Name);
                            table.Cell().Element(VoucherBodyCell).AlignCenter().Text(line.UnitMeasure ?? "—");
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.Quantity.ToString("N2", ReportCulture));
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.UnitCost.ToString("N2", ReportCulture));
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.TotalCost.ToString("N2", ReportCulture));
                        }

                        table.Cell().ColumnSpan(6).Element(VoucherTotalCell).AlignRight().Text("TOTALES (Bs.)").Bold();
                        table.Cell().Element(VoucherTotalCell).AlignRight().Text(total.ToString("N2", ReportCulture)).Bold();
                    });

                    mainColumn.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(c => SignatureBox(c, "Autorizado Por:", null));
                        row.ConstantItem(20);
                        row.RelativeItem().Column(c => SignatureBox(c, "Recibí Conforme:", clientName));
                        row.ConstantItem(20);
                        row.RelativeItem().Column(c => SignatureBox(c, "Responsable de Inventarios:", inventoryManagerName));
                    });
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });
    }

    // Rollo térmico angosto (80mm) para "Factura"/"Recibo" — solo el estilo visual del ejemplo
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
                page.DefaultTextStyle(x => x.FontSize(8));

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
                        t.Span("N° Salida: ").Bold();
                        t.Span(issueId.ToString());
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
                            table.Cell().PaddingTop(1).Text(line.Quantity.ToString("N2", ReportCulture)).FontSize(7);
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

                    mainColumn.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Black);
                    mainColumn.Item().PaddingTop(3).AlignCenter().Text("Comprobante interno de salida de almacén.").FontSize(6);
                    mainColumn.Item().AlignCenter().Text("No es válido como factura fiscal.").FontSize(6);
                });

                page.Footer().PaddingTop(4).Column(c =>
                {
                    c.Item().AlignCenter().Text($"Usuario: {username}").FontSize(6);
                    c.Item().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy HH:mm}").FontSize(6);
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

        // El formulario de "Nueva transferencia" solo pide un Cliente como destinatario
        // (ReceiverClientId) — ReceiverUserId existe en el esquema pero no lo llena esta
        // pantalla; se deja como respaldo por si algún día se usa.
        var receiverName = "—";
        var receiverEmail = "—";
        if (transfer.ReceiverClientId.HasValue)
        {
            var client = await _unitOfWork.Repository<Client>().GetByIdAsync(transfer.ReceiverClientId.Value);
            if (client is not null)
            {
                var party = await _unitOfWork.Repository<Party>().GetByIdAsync(client.PartyId);
                receiverName = party?.Name ?? receiverName;
                receiverEmail = party?.Email ?? receiverEmail;
            }
        }
        else if (transfer.ReceiverUserId.HasValue)
        {
            var receiver = await _unitOfWork.Repository<AppUser>().GetByIdAsync(transfer.ReceiverUserId.Value);
            if (receiver is not null)
            {
                receiverName = $"{receiver.FirstName} {receiver.LastName}".Trim();
                receiverEmail = receiver.Email;
            }
        }

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var total = lines.Sum(l => l.TotalSalePrice);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.ConstantItem(65).Height(48).Image(GetLogoBytes()).FitArea();
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(10);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(10);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(10);
                        });
                        row.ConstantItem(110).Height(48).Border(1).BorderColor(Colors.Grey.Darken1).Padding(6).Column(c =>
                        {
                            c.Item().AlignCenter().Text("TRANSFERENCIA N°").FontSize(7).FontColor(Colors.Grey.Darken2);
                            c.Item().AlignCenter().Text(transferId.ToString()).Bold().FontSize(16);
                        });
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("TRANSFERENCIA DE ALMACÉN").Bold().FontSize(14);
                });

                page.Content().PaddingTop(10).Column(mainColumn =>
                {
                    mainColumn.Item().PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("ALMACÉN DE ORIGEN: ").Bold();
                            t.Span(sourceWarehouseName);
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("FECHA: ").Bold();
                            t.Span(transfer.TransferDate.ToString("dd/MM/yyyy"));
                        });
                    });

                    mainColumn.Item().PaddingBottom(6).Text(t =>
                    {
                        t.Span("ALMACÉN DE DESTINO: ").Bold();
                        t.Span(destinationWarehouseName);
                    });

                    mainColumn.Item().PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("ENVÍA: ").Bold();
                            t.Span(senderName);
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("CORREO: ").Bold();
                            t.Span(senderEmail);
                        });
                    });

                    mainColumn.Item().PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("RECIBE: ").Bold();
                            t.Span(receiverName);
                        });
                        row.RelativeItem().Text(t =>
                        {
                            t.Span("CORREO: ").Bold();
                            t.Span(receiverEmail);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(transfer.Notes))
                    {
                        mainColumn.Item().PaddingBottom(6).Text(t =>
                        {
                            t.Span("OBSERVACIONES: ").Bold();
                            t.Span(transfer.Notes);
                        });
                    }

                    mainColumn.Item().PaddingTop(6).Table(table =>
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
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("PRECIO DE\nVENTA (Bs.)");
                            header.Cell().Element(VoucherHeaderCell).AlignCenter().Text("TOTAL\nP.V.P. (Bs.)");
                        });

                        foreach (var line in lines)
                        {
                            table.Cell().Element(VoucherBodyCell).Text(line.Code ?? "—");
                            table.Cell().Element(VoucherBodyCell).Text(line.DepartmentName ?? "—");
                            table.Cell().Element(VoucherBodyCell).Text(line.Name);
                            table.Cell().Element(VoucherBodyCell).AlignCenter().Text(line.UnitMeasure ?? "—");
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.Quantity.ToString("N2", ReportCulture));
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.UnitPrice.ToString("N2", ReportCulture));
                            table.Cell().Element(VoucherBodyCell).AlignRight().Text(line.TotalSalePrice.ToString("N2", ReportCulture));
                        }

                        table.Cell().ColumnSpan(6).Element(VoucherTotalCell).AlignRight().Text("TOTAL (Bs.)").Bold();
                        table.Cell().Element(VoucherTotalCell).AlignRight().Text(total.ToString("N2", ReportCulture)).Bold();
                    });

                    mainColumn.Item().PaddingTop(40).Row(row =>
                    {
                        row.RelativeItem().Column(c => SignatureBox(c, "Envía:", senderName));
                        row.ConstantItem(20);
                        row.RelativeItem().Column(c => SignatureBox(c, "Recibe:", receiverName));
                    });
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    // Grid completo (no solo borde inferior como HeaderCell/BodyCell) para que el comprobante
    // de impresión de una Entrada se vea como una tabla formal impresa, igual que el formato
    // en papel que ya usa el cliente — a diferencia de los reportes agregados, que son listas
    // largas donde un grid completo sería visualmente pesado.
    private static IContainer VoucherHeaderCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten3).Padding(4).MinHeight(28).DefaultTextStyle(x => x.Bold().FontSize(7));

    private static IContainer VoucherBodyCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Darken1).Padding(4).DefaultTextStyle(x => x.FontSize(8));

    private static IContainer VoucherTotalCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Black).Background(Colors.Grey.Lighten2).Padding(4).DefaultTextStyle(x => x.FontSize(8));

    private static void SignatureBox(QuestPDF.Fluent.ColumnDescriptor column, string label, string? name)
    {
        column.Item().Text(label).Bold().FontSize(9);
        column.Item().Height(28);
        column.Item().BorderTop(1).BorderColor(Colors.Grey.Darken1).PaddingTop(2).AlignCenter().Text(name ?? "").FontSize(8);
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
        return await _unitOfWork.SqlQueryAsync<KardexByProductReportRowDto>(
            GetKardexByProductReportSql,
            filter.ProductId,
            filter.WarehouseId,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);
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

        var rows = await GetKardexByProductReport(filter);
        var opening = await GetKardexByProductOpeningBalance(filter.ProductId, filter.WarehouseId, filter.DateFrom);

        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(filter.ProductId);
        var productLabel = product is null ? "—" : $"{product.Code} - {product.Name}";

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
                page.DefaultTextStyle(x => x.FontSize(8));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("KARDEX POR MATERIAL").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Producto: {productLabel}").FontSize(10);
                    column.Item().AlignCenter().Text($"Almacén: {warehouseName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Periodo: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

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
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Valor\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                    });

                    var openingUnitValue = opening.OpeningQuantity == 0 ? 0 : opening.OpeningValue / opening.OpeningQuantity;

                    table.Cell().Element(BodyCell).Text("");
                    table.Cell().Element(BodyCell).Text("Saldo Inicial").Bold();
                    table.Cell().Element(BodyCell).Text("");
                    table.Cell().Element(BodyCell).Text("");
                    table.Cell().Element(BodyCell).Text("");
                    table.Cell().Element(BodyCell).Text("");
                    table.Cell().Element(BodyCell).Text("");
                    table.Cell().Element(BodyCell).Text("");
                    table.Cell().Element(BodyCell).AlignRight().Text(opening.OpeningQuantity.ToString("N2", ReportCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(openingUnitValue.ToString("N2", ReportCulture));
                    table.Cell().Element(BodyCell).AlignRight().Text(opening.OpeningValue.ToString("N2", ReportCulture));

                    foreach (var row in rows)
                    {
                        var isReceipt = row.MovementType == "receipt";
                        var balanceUnitValue = row.BalanceQuantity == 0 ? 0 : row.BalanceValue / row.BalanceQuantity;

                        table.Cell().Element(BodyCell).Text(row.VoucherDate.ToString("dd/MM/yyyy"));
                        table.Cell().Element(BodyCell).Text(row.VoucherId.ToString().PadLeft(5, '0'));
                        table.Cell().Element(BodyCell).AlignRight().Text(isReceipt ? row.Quantity.ToString("N2", ReportCulture) : "");
                        table.Cell().Element(BodyCell).AlignRight().Text(isReceipt ? row.UnitCost.ToString("N2", ReportCulture) : "");
                        table.Cell().Element(BodyCell).AlignRight().Text(isReceipt ? row.TotalCost.ToString("N2", ReportCulture) : "");
                        table.Cell().Element(BodyCell).AlignRight().Text(!isReceipt ? row.Quantity.ToString("N2", ReportCulture) : "");
                        table.Cell().Element(BodyCell).AlignRight().Text(!isReceipt ? row.UnitCost.ToString("N2", ReportCulture) : "");
                        table.Cell().Element(BodyCell).AlignRight().Text(!isReceipt ? row.TotalCost.ToString("N2", ReportCulture) : "");
                        table.Cell().Element(BodyCell).AlignRight().Text(row.BalanceQuantity.ToString("N2", ReportCulture));
                        table.Cell().Element(BodyCell).AlignRight().Text(balanceUnitValue.ToString("N2", ReportCulture));
                        table.Cell().Element(BodyCell).AlignRight().Text(row.BalanceValue.ToString("N2", ReportCulture));
                    }
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("REPORTE CAMPO PASTOR").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Pastor / Cliente: {clientName}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

                page.Content().PaddingTop(10).Column(mainColumn =>
                {
                    mainColumn.Spacing(16);

                    mainColumn.Item().Column(section =>
                    {
                        section.Item().Text("Cuentas por Cobrar").Bold().FontSize(12);

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
                                header.Cell().Element(HeaderCell).Text("N° Salida");
                                header.Cell().Element(HeaderCell).Text("Fecha");
                                header.Cell().Element(HeaderCell).Text("Fecha Límite");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Saldo Pendiente");
                                header.Cell().Element(HeaderCell).Text("Estado");
                            });

                            foreach (var row in data.Receivables)
                            {
                                table.Cell().Element(BodyCell).Text(row.IssueId.HasValue ? row.IssueId.Value.ToString().PadLeft(5, '0') : "—");
                                table.Cell().Element(BodyCell).Text(row.IssueDate.HasValue ? row.IssueDate.Value.ToString("dd/MM/yyyy") : "—");
                                table.Cell().Element(BodyCell).Text(row.DueDate.HasValue ? row.DueDate.Value.ToString("dd/MM/yyyy") : "—");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.OutstandingBalance.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).Text(row.Status == "paid" ? "Pagado" : "Pendiente");
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
                        section.Item().Text("Entregas").Bold().FontSize(12);

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
                                header.Cell().Element(HeaderCell).Text("N° Salida");
                                header.Cell().Element(HeaderCell).Text("Fecha");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                            });

                            foreach (var row in data.Deliveries)
                            {
                                table.Cell().Element(BodyCell).Text(row.IssueId.ToString().PadLeft(5, '0'));
                                table.Cell().Element(BodyCell).Text(row.IssueDate.ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Total.ToString("N2", ReportCulture));
                            }
                        });
                    });

                    mainColumn.Item().Column(section =>
                    {
                        section.Item().Text("Depósitos").Bold().FontSize(12);

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
                                header.Cell().Element(HeaderCell).Text("N° Salida");
                                header.Cell().Element(HeaderCell).AlignRight().Text("Monto");
                                header.Cell().Element(HeaderCell).Text("Método");
                                header.Cell().Element(HeaderCell).Text("Notas");
                            });

                            foreach (var row in data.Deposits)
                            {
                                table.Cell().Element(BodyCell).Text(row.PaymentDate.ToString("dd/MM/yyyy"));
                                table.Cell().Element(BodyCell).Text(row.IssueId.HasValue ? row.IssueId.Value.ToString().PadLeft(5, '0') : "—");
                                table.Cell().Element(BodyCell).AlignRight().Text(row.Amount.ToString("N2", ReportCulture));
                                table.Cell().Element(BodyCell).Text(row.PaymentMethod ?? "—");
                                table.Cell().Element(BodyCell).Text(row.Notes ?? "");
                            }
                        });
                    });
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    private const string GetAccountReceivablesReportSql = """
        SELECT
            account_receivable_id AS "AccountReceivableId",
            issue_id AS "IssueId",
            issue_date AS "IssueDate",
            client_id AS "ClientId",
            client_name AS "ClientName",
            total_amount AS "TotalAmount",
            outstanding_balance AS "OutstandingBalance",
            due_date AS "DueDate",
            status AS "Status"
        FROM sp_get_account_receivables_report({0}::bigint, {1}::varchar, {2}::date, {3}::date)
        """;

    public async Task<List<AccountReceivablesReportRowDto>> GetAccountReceivablesReport(AccountReceivablesReportQueryFilter filter)
    {
        return await _unitOfWork.SqlQueryAsync<AccountReceivablesReportRowDto>(
            GetAccountReceivablesReportSql,
            (object?)filter.ClientId ?? DBNull.Value,
            (object?)filter.Status ?? DBNull.Value,
            (object?)filter.DateFrom ?? DBNull.Value,
            (object?)filter.DateTo ?? DBNull.Value);
    }

    public async Task<byte[]> GenerateAccountReceivablesReportPdf(AccountReceivablesReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetAccountReceivablesReport(filter);

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
            "pending" => "Pendientes",
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

        var pending = rows.Where(r => r.Status != "paid").ToList();
        var totalOutstanding = pending.Sum(r => r.OutstandingBalance);
        var totalPortfolio = rows.Sum(r => r.TotalAmount);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("IGLESIA ADVENTISTA DEL SÉPTIMO DÍA").Bold().FontSize(11);
                            c.Item().Text("MISIÓN BOLIVIANA OCCIDENTAL DEL SUR").Bold().FontSize(11);
                            c.Item().Text("LA PAZ - BOLIVIA").Bold().FontSize(11);
                        });
                        row.ConstantItem(140).Height(45).Image(GetLogoBytes()).FitArea();
                    });

                    column.Item().PaddingTop(10).AlignCenter().Text("REPORTE GENERAL DE CUENTAS POR COBRAR").Bold().FontSize(14);
                    column.Item().AlignCenter().Text($"Cliente: {clientName}").FontSize(10);
                    column.Item().AlignCenter().Text($"Estado: {statusLabel} — Fecha límite: {periodText}").FontSize(10);
                    column.Item().AlignCenter().Text("(Expresado en Bolivianos)").FontSize(9).Italic();
                });

                page.Content().PaddingTop(10).Column(mainColumn =>
                {
                    mainColumn.Spacing(10);

                    mainColumn.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2.2f);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("Cliente");
                            header.Cell().Element(HeaderCell).Text("N° Salida");
                            header.Cell().Element(HeaderCell).Text("Fecha");
                            header.Cell().Element(HeaderCell).Text("Fecha\nLímite");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Total");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Saldo\nPendiente");
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Estado");
                        });

                        foreach (var row in rows)
                        {
                            table.Cell().Element(BodyCell).Text(row.ClientName ?? "—");
                            table.Cell().Element(BodyCell).Text(row.IssueId.HasValue ? row.IssueId.Value.ToString().PadLeft(5, '0') : "—");
                            table.Cell().Element(BodyCell).Text(row.IssueDate.HasValue ? row.IssueDate.Value.ToString("dd/MM/yyyy") : "—");
                            table.Cell().Element(BodyCell).Text(row.DueDate.HasValue ? row.DueDate.Value.ToString("dd/MM/yyyy") : "—");
                            table.Cell().Element(BodyCell).AlignRight().Text(row.TotalAmount.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.OutstandingBalance.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignCenter().Text(row.Status == "paid" ? "Pagado" : "Pendiente");
                        }

                        table.Cell().ColumnSpan(4).Element(CategoryTotalCell).AlignRight().Text("Total Cartera").Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(totalPortfolio.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).AlignRight().Text(totalOutstanding.ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(CategoryTotalCell).Text("");
                    });

                    mainColumn.Item().PaddingTop(8).Row(row =>
                    {
                        row.Spacing(10);
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Total Pendiente", totalOutstanding.ToString("N2", ReportCulture)));
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Cuentas Pendientes", pending.Count.ToString()));
                        row.RelativeItem().Element(c => SummaryTextCard(c, "Total Cartera", totalPortfolio.ToString("N2", ReportCulture)));
                    });
                });

                page.Footer().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text($"Usuario: {username}").FontSize(8);
                    row.RelativeItem().AlignCenter().Text($"{DateTime.UtcNow:dd/MM/yyyy}").FontSize(8);
                    row.RelativeItem().AlignRight().Text(x =>
                    {
                        x.Span("Pág. ").FontSize(8);
                        x.CurrentPageNumber().FontSize(8);
                    });
                });
            });
        });

        return document.GeneratePdf();
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

    // Grid completo + alto mínimo uniforme para que los encabezados de 1 y de 2 líneas
    // queden centrados a la misma altura (mismo motivo por el que se hizo en los
    // comprobantes de impresión: que la tabla se vea formal y simétrica).
    private static IContainer HeaderCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Lighten3).Padding(4).MinHeight(24).AlignMiddle().DefaultTextStyle(x => x.Bold());

    private static IContainer BodyCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten2).Padding(3);

    private static IContainer SubtotalCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten2).Padding(3);

    private static IContainer CategoryTotalCell(IContainer container) =>
        container.Border(1).BorderColor(Colors.Grey.Darken1).Background(Colors.Grey.Medium).Padding(3);

    private static void SummaryCard(QuestPDF.Infrastructure.IContainer container, string label, decimal amount)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten5)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken2);
                column.Item().Text(amount.ToString("N2", ReportCulture)).FontSize(16).Bold();
            });
    }

    private static void SummaryTextCard(QuestPDF.Infrastructure.IContainer container, string label, string value)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten5)
            .Padding(10)
            .Column(column =>
            {
                column.Item().Text(label).FontSize(9).FontColor(Colors.Grey.Darken2);
                column.Item().Text(value).FontSize(14).Bold();
            });
    }

    private async Task<FinancialDashboardSummaryDto> GetSummaryAsync()
    {
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthEnd = monthStart.AddMonths(1);

        var totalIssues = await (
            from d in _unitOfWork.Repository<IssueDetail>().Query()
            join i in _unitOfWork.Repository<Issue>().Query() on d.IssueId equals i.IssueId
            where i.IssueDate >= monthStart && i.IssueDate < monthEnd
            select d.TotalCost
        ).SumAsync(x => (decimal?)x) ?? 0;

        var totalReceipts = await (
            from d in _unitOfWork.Repository<ReceiptDetail>().Query()
            join r in _unitOfWork.Repository<Receipt>().Query() on d.ReceiptId equals r.ReceiptId
            where r.IssueDate >= monthStart && r.IssueDate < monthEnd
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
        var now = DateTime.UtcNow;
        var rangeStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(MonthsBack - 1));

        var raw = await (
            from d in _unitOfWork.Repository<IssueDetail>().Query()
            join i in _unitOfWork.Repository<Issue>().Query() on d.IssueId equals i.IssueId
            where i.IssueDate >= rangeStart
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

    private static byte[] BuildIssuesByMonthChart(List<MonthlyTotalDto> monthlyTotals)
    {
        var plot = new ScottPlot.Plot();
        var values = monthlyTotals.Select(m => (double)m.Total).ToArray();
        var positions = Enumerable.Range(0, values.Length).Select(i => (double)i).ToArray();

        plot.Add.Bars(positions, values);
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            positions, monthlyTotals.Select(m => m.MonthLabel).ToArray());
        plot.Axes.Bottom.MajorTickStyle.Length = 0;
        plot.Title("Salidas por mes");
        plot.YLabel("Total (Bs.)");

        return plot.GetImage(900, 450).GetImageBytes(ScottPlot.ImageFormat.Png, 100);
    }

    private static byte[] BuildReceivableStatusChart(ReceivableStatusSummaryDto status)
    {
        var plot = new ScottPlot.Plot();
        double[] values = [(double)status.PendingTotal, (double)status.PaidTotal];
        var positions = new double[] { 0, 1 };
        var bars = plot.Add.Bars(positions, values);
        bars.Bars[0].FillColor = ScottPlot.Colors.OrangeRed;
        bars.Bars[1].FillColor = ScottPlot.Colors.SeaGreen;

        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            positions, ["Pendiente", "Pagado"]);
        plot.Axes.Bottom.MajorTickStyle.Length = 0;
        plot.Title("Cuentas por Cobrar");
        plot.YLabel("Total (Bs.)");

        return plot.GetImage(900, 450).GetImageBytes(ScottPlot.ImageFormat.Png, 100);
    }
}
