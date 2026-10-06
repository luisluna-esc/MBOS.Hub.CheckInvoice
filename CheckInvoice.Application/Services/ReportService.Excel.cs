using System.Globalization;
using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.QueryFilters.Reports;
using ClosedXML.Excel;

namespace CheckInvoice.Application.Services;

// Versión Excel (.xlsx, ClosedXML) de los reportes con datos. Usa las mismas consultas que el
// PDF, pero en vez de copiar su diseño (grupos con subtotales intercalados) arma una tabla plana:
// una fila por registro, con Departamento/Subdepartamento como columnas, filtros en el encabezado
// y totales con SUBTOTAL(9, ...), que se recalculan solos al filtrar. Así se puede ordenar,
// filtrar y armar tablas dinámicas, que es para lo que se baja un Excel.
public partial class ReportService
{
    private const string MoneyFormat = "#,##0.00";
    private const string DateFormat = "dd/mm/yyyy";
    private const string IdFormat = "00000";

    private sealed record ExcelColumn<T>(
        string Header,
        Func<T, object?> Value,
        string? Format = null,
        bool Total = false,
        decimal? FixedTotal = null);

    public async Task<byte[]> GenerateStockReportExcel(StockReportQueryFilter filter)
    {
        var rows = (await GetStockReport(filter)).OrderBy(r => r.DepartmentName).ThenBy(r => r.SubDepartmentName).ThenBy(r => r.Name).ToList();
        var info = new List<string>
        {
            $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}",
            $"Periodo: {PeriodText(filter.DateFrom, filter.DateTo)}"
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Inventario general", "INVENTARIO GENERAL", info, StockColumns(), rows));
    }

    public async Task<byte[]> GenerateStockByDepartmentReportExcel(StockByDepartmentReportQueryFilter filter)
    {
        var rows = (await GetStockByDepartmentReport(filter)).OrderBy(r => r.SubDepartmentName).ThenBy(r => r.Name).ToList();

        var departmentName = "Todas las categorías";
        if (filter.DepartmentId.HasValue)
        {
            var department = await _unitOfWork.Repository<Department>().GetByIdAsync(filter.DepartmentId.Value);
            departmentName = department?.Name ?? departmentName;
        }

        var info = new List<string>
        {
            $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}",
            $"Categoría: {departmentName}",
            $"Periodo: {PeriodText(filter.DateFrom, filter.DateTo)}"
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Inventario por departamento", "INVENTARIO POR DEPARTAMENTO", info, StockColumns(), rows));
    }

    public async Task<byte[]> GenerateKardexReportExcel(KardexReportQueryFilter filter)
    {
        var rows = (await GetKardexReport(filter)).OrderBy(r => r.SubDepartmentName).ThenBy(r => r.Name).ToList();
        var info = new List<string>
        {
            $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}",
            $"Periodo: {PeriodText(filter.DateFrom, filter.DateTo)}"
        };
        var columns = new List<ExcelColumn<KardexReportRowDto>>
        {
            new("Subdepartamento", r => r.SubDepartmentName ?? "(Sin subcategoría)"),
            new("Código", r => r.Code),
            new("Descripción", r => r.Name),
            new("Saldo inicial cant.", r => r.OpeningQuantity, MoneyFormat, Total: true),
            new("Saldo inicial valor", r => r.OpeningValue, MoneyFormat, Total: true),
            new("Entradas cant.", r => r.ReceiptsQuantity, MoneyFormat, Total: true),
            new("Entradas valor", r => r.ReceiptsValue, MoneyFormat, Total: true),
            new("Salidas cant.", r => r.IssuesQuantity, MoneyFormat, Total: true),
            new("Salidas valor", r => r.IssuesValue, MoneyFormat, Total: true),
            new("Saldo cant.", r => r.ClosingQuantity, MoneyFormat, Total: true),
            new("Saldo valor", r => r.ClosingValue, MoneyFormat, Total: true)
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Kardex", "KARDEX FÍSICO VALORADO", info, columns, rows));
    }

    public async Task<byte[]> GenerateInventoryCountReportExcel(InventoryCountReportQueryFilter filter)
    {
        var rows = (await GetInventoryCountReport(filter)).OrderBy(r => r.DepartmentName).ThenBy(r => r.SubDepartmentName).ThenBy(r => r.Name).ToList();
        var info = new List<string>
        {
            $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}",
            $"Periodo: {PeriodText(filter.DateFrom, filter.DateTo)}"
        };
        var columns = new List<ExcelColumn<InventoryCountReportRowDto>>
        {
            new("Departamento", r => r.DepartmentName ?? "(Sin categoría)"),
            new("Subdepartamento", r => r.SubDepartmentName ?? "(Sin subcategoría)"),
            new("Código", r => r.Code),
            new("Descripción", r => r.Name),
            new("Unidad de medida", r => r.UnitMeasure),
            new("Cantidad", r => r.Quantity, MoneyFormat, Total: true),
            new("Valor unitario", r => r.UnitValue ?? 0, MoneyFormat),
            new("Valor total", r => r.TotalValue, MoneyFormat, Total: true),
            new("Cantidad física", r => r.PhysicalQuantity ?? 0, MoneyFormat, Total: true),
            new("Diferencia", r => r.Difference ?? 0, MoneyFormat, Total: true)
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Levantamiento", "LEVANTAMIENTO DE INVENTARIO", info, columns, rows));
    }

    public async Task<byte[]> GenerateIssuesReportExcel(IssuesReportQueryFilter filter)
    {
        var rows = (await GetIssuesReport(filter))
            .OrderBy(r => r.DepartmentName).ThenBy(r => r.SubDepartmentName).ThenBy(r => r.Name).ThenBy(r => r.IssueId).ToList();
        var info = new List<string>
        {
            $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}",
            $"Periodo: {PeriodText(filter.DateFrom, filter.DateTo)}"
        };
        var columns = new List<ExcelColumn<IssuesReportRowDto>>
        {
            new("N° Salida", r => r.IssueId, IdFormat),
            new("Departamento", r => r.DepartmentName ?? "(Sin categoría)"),
            new("Subdepartamento", r => r.SubDepartmentName ?? "(Sin subcategoría)"),
            new("Código", r => r.Code),
            new("Descripción", r => r.Name),
            new("Unidad de medida", r => r.UnitMeasure),
            new("Cantidad", r => r.Quantity, MoneyFormat, Total: true),
            new("Valor unitario", r => r.UnitCost, MoneyFormat),
            new("Valor total", r => r.TotalCost, MoneyFormat, Total: true)
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Salidas", "SALIDAS DE ALMACÉN", info, columns, rows));
    }

    public async Task<byte[]> GenerateReceiptsReportExcel(ReceiptsReportQueryFilter filter)
    {
        var rows = (await GetReceiptsReport(filter))
            .OrderBy(r => r.DepartmentName).ThenBy(r => r.SubDepartmentName).ThenBy(r => r.Name).ThenBy(r => r.ReceiptId).ToList();
        var info = new List<string>
        {
            $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}",
            $"Periodo: {PeriodText(filter.DateFrom, filter.DateTo)}"
        };
        var columns = new List<ExcelColumn<ReceiptsReportRowDto>>
        {
            new("N° Ingreso", r => r.ReceiptId, IdFormat),
            new("Fecha", r => r.ReceiptDate.Date, DateFormat),
            new("Departamento", r => r.DepartmentName ?? "(Sin categoría)"),
            new("Subdepartamento", r => r.SubDepartmentName ?? "(Sin subcategoría)"),
            new("Código", r => r.Code),
            new("Descripción", r => r.Name),
            new("Unidad de medida", r => r.UnitMeasure),
            new("Cantidad", r => r.Quantity, MoneyFormat, Total: true),
            new("Valor unitario", r => r.UnitCost, MoneyFormat),
            new("Valor total", r => r.TotalCost, MoneyFormat, Total: true)
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Ingresos", "INGRESOS DE ALMACÉN", info, columns, rows));
    }

    private sealed record KardexMovementExcelRow(
        DateTime? Date, string Voucher, string Type,
        decimal? InQuantity, decimal? InUnitCost, decimal? InTotal,
        decimal? OutQuantity, decimal? OutUnitCost, decimal? OutTotal,
        decimal BalanceQuantity, decimal BalanceUnitValue, decimal BalanceValue);

    public async Task<byte[]> GenerateKardexByProductReportExcel(KardexByProductReportQueryFilter filter)
    {
        var rows = await GetKardexByProductReport(filter);
        var opening = await GetKardexByProductOpeningBalance(filter.ProductId, filter.WarehouseId, filter.DateFrom);

        var product = await _unitOfWork.Repository<Product>().GetByIdAsync(filter.ProductId);
        var productLabel = product is null ? "—" : $"{product.Code} - {product.Name}";

        var movements = new List<KardexMovementExcelRow>
        {
            new(null, "Saldo inicial", "", null, null, null, null, null, null,
                opening.OpeningQuantity,
                opening.OpeningQuantity == 0 ? 0 : opening.OpeningValue / opening.OpeningQuantity,
                opening.OpeningValue)
        };
        foreach (var row in rows)
        {
            var isReceipt = row.MovementType == "receipt";
            movements.Add(new KardexMovementExcelRow(
                row.VoucherDate.Date,
                row.VoucherId.ToString().PadLeft(5, '0'),
                isReceipt ? "Ingreso" : "Salida",
                isReceipt ? row.Quantity : null, isReceipt ? row.UnitCost : null, isReceipt ? row.TotalCost : null,
                isReceipt ? null : row.Quantity, isReceipt ? null : row.UnitCost, isReceipt ? null : row.TotalCost,
                row.BalanceQuantity,
                row.BalanceQuantity == 0 ? 0 : row.BalanceValue / row.BalanceQuantity,
                row.BalanceValue));
        }

        var info = new List<string>
        {
            $"Producto: {productLabel}",
            $"Almacén: {await WarehouseNameAsync(filter.WarehouseId)}",
            $"Periodo: {PeriodText(filter.DateFrom, filter.DateTo)}"
        };
        var columns = new List<ExcelColumn<KardexMovementExcelRow>>
        {
            new("Fecha", r => r.Date, DateFormat),
            new("N° Comprobante", r => r.Voucher),
            new("Tipo", r => r.Type),
            new("Ingresos cant.", r => r.InQuantity, MoneyFormat, Total: true),
            new("Ingresos valor unit.", r => r.InUnitCost, MoneyFormat),
            new("Ingresos total", r => r.InTotal, MoneyFormat, Total: true),
            new("Salidas cant.", r => r.OutQuantity, MoneyFormat, Total: true),
            new("Salidas valor unit.", r => r.OutUnitCost, MoneyFormat),
            new("Salidas total", r => r.OutTotal, MoneyFormat, Total: true),
            new("Saldo cant.", r => r.BalanceQuantity, MoneyFormat),
            new("Saldo valor unit.", r => r.BalanceUnitValue, MoneyFormat),
            new("Saldo total", r => r.BalanceValue, MoneyFormat)
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Kardex por material", "KARDEX POR MATERIAL", info, columns, movements));
    }

    public async Task<byte[]> GeneratePastorFieldReportExcel(PastorFieldReportQueryFilter filter)
    {
        var data = await GetPastorFieldReportData(filter);
        var clientName = await ClientNameAsync(filter.ClientId) ?? "—";
        var period = $"Periodo de entregas y depósitos: {PeriodText(filter.DateFrom, filter.DateTo)}";

        var receivablesInfo = new List<string>
        {
            $"Pastor / Cliente: {clientName}",
            $"Total pendiente: {data.TotalOutstanding.ToString("N2", ReportCulture)} — Cuentas pendientes: {data.PendingCount}",
            $"Fecha de última entrega: {(data.LastDeliveryDate.HasValue ? data.LastDeliveryDate.Value.ToString("dd/MM/yyyy") : "—")}"
        };
        var receivableColumns = new List<ExcelColumn<PastorFieldReceivableRowDto>>
        {
            new("N° Salida", r => r.IssueId, IdFormat),
            new("Fecha", r => r.IssueDate?.Date, DateFormat),
            new("Fecha límite", r => r.DueDate?.ToDateTime(TimeOnly.MinValue), DateFormat),
            new("Saldo pendiente", r => r.OutstandingBalance, MoneyFormat, Total: true),
            new("Estado", r => AccountReceivableStatusLabel(r.Status))
        };
        var deliveryColumns = new List<ExcelColumn<PastorFieldDeliveryRowDto>>
        {
            new("N° Salida", r => r.IssueId, IdFormat),
            new("Fecha", r => r.IssueDate.Date, DateFormat),
            new("Total", r => r.Total, MoneyFormat, Total: true)
        };
        var depositColumns = new List<ExcelColumn<PastorFieldDepositRowDto>>
        {
            // Momento exacto del pago (UTC) pasado a la hora de Bolivia.
            new("Fecha", r => BoliviaTime.FromUtc(r.PaymentDate).Date, DateFormat),
            new("N° Salida", r => r.IssueId, IdFormat),
            new("Monto", r => r.Amount, MoneyFormat, Total: true),
            new("Método", r => PaymentMethodLabel(r.PaymentMethod)),
            new("Notas", r => r.Notes)
        };

        return await BuildWorkbookAsync(wb =>
        {
            AddSheet(wb, "Cuentas por cobrar", "REPORTE CAMPO PASTOR: CUENTAS POR COBRAR", receivablesInfo, receivableColumns, data.Receivables);
            AddSheet(wb, "Entregas", "REPORTE CAMPO PASTOR: ENTREGAS", [$"Pastor / Cliente: {clientName}", period], deliveryColumns, data.Deliveries);
            AddSheet(wb, "Depósitos", "REPORTE CAMPO PASTOR: DEPÓSITOS", [$"Pastor / Cliente: {clientName}", period], depositColumns, data.Deposits);
        });
    }

    public async Task<byte[]> GenerateAccountReceivablesReportExcel(AccountReceivablesReportQueryFilter filter)
    {
        var accounts = await GetAccountReceivablesReport(filter);

        // Mismo contenido que el PDF: una fila por producto con lo que Caja registró de él.
        var productRows = (await GetReceivableProductRowsAsync(accounts))
            .OrderBy(r => r.Account.ClientName).ThenBy(r => r.Account.IssueId).ThenBy(r => r.Name)
            .ToList();

        var statusLabel = filter.Status switch
        {
            "open" => "Por cobrar (pendientes y con pago retrasado)",
            "pending" => "Pendientes",
            "late" => "Pago retrasado",
            "paid" => "Pagadas",
            _ => "Todos los estados"
        };
        var info = new List<string>
        {
            $"Cliente: {(filter.ClientId.HasValue ? await ClientNameAsync(filter.ClientId.Value) : null) ?? "Todos los clientes"}",
            $"Estado: {statusLabel} — Fecha límite: {PeriodText(filter.DateFrom, filter.DateTo)}",
            $"Cuentas pendientes: {accounts.Count(a => a.Status != "paid")}"
        };

        var columns = new List<ExcelColumn<ReceivableProductRow>>
        {
            new("Cliente", r => r.Account.ClientName ?? "(Sin cliente)"),
            new("N° Salida", r => r.Account.IssueId, IdFormat),
            new("Fecha", r => r.Account.IssueDate?.Date, DateFormat),
            new("Fecha límite", r => r.Account.DueDate?.ToDateTime(TimeOnly.MinValue), DateFormat)
        };
        columns.AddRange(
        [
            new("Código", r => r.Code),
            new("Descripción", r => r.Name),
            new("Unidad de medida", r => r.UnitMeasure),
            new("Cantidad", r => r.Quantity, MoneyFormat, Total: true),
            new("Valor unitario", r => r.UnitCost, MoneyFormat),
            new("Valor total", r => r.TotalCost, MoneyFormat, Total: true),
            new("Pagado", r => r.Paid, MoneyFormat, Total: true),
            new("Saldo", r => r.Balance, MoneyFormat, Total: true),
            new("Estado", r => AccountReceivableStatusLabel(r.Account.Status))
        ]);

        return await BuildWorkbookAsync(wb => AddSheet(wb, "Cuentas por cobrar", "REPORTE GENERAL DE CUENTAS POR COBRAR", info, columns, productRows));
    }

    private static List<ExcelColumn<StockReportRowDto>> StockColumns() =>
    [
        new("Departamento", r => r.DepartmentName ?? "(Sin categoría)"),
        new("Subdepartamento", r => r.SubDepartmentName ?? "(Sin subcategoría)"),
        new("Código", r => r.Code),
        new("Descripción", r => r.Name),
        new("Unidad de medida", r => r.UnitMeasure),
        new("Cantidad", r => r.NetQuantity, MoneyFormat),
        new("Valor unitario", r => r.UnitValue, MoneyFormat),
        new("Valor total", r => r.TotalValue, MoneyFormat, Total: true)
    ];

    // ---------- Armado del libro ----------

    private string _excelUsername = "—";

    private async Task<byte[]> BuildWorkbookAsync(Action<XLWorkbook> fill)
    {
        _excelUsername = await CurrentUsernameAsync();

        using var workbook = new XLWorkbook();
        fill(workbook);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private void AddSheet<T>(
        XLWorkbook workbook, string sheetName, string title, IReadOnlyList<string> info,
        IReadOnlyList<ExcelColumn<T>> columns, IReadOnlyList<T> rows)
    {
        var sheet = workbook.Worksheets.Add(sheetName);
        var columnCount = columns.Count;
        var widths = columns.Select(c => (double)c.Header.Length).ToArray();

        var headerLines = new List<string>
        {
            "IGLESIA ADVENTISTA DEL SÉPTIMO DÍA — MISIÓN BOLIVIANA OCCIDENTAL DEL SUR — LA PAZ - BOLIVIA",
            title
        };
        headerLines.AddRange(info);
        headerLines.Add("(Expresado en Bolivianos)");
        headerLines.Add($"Generado el {BoliviaTime.Now:dd/MM/yyyy HH:mm} por {_excelUsername}");

        var rowIndex = 1;
        foreach (var line in headerLines)
        {
            var cell = sheet.Cell(rowIndex, 1);
            cell.Value = line;
            sheet.Range(rowIndex, 1, rowIndex, columnCount).Merge();
            if (rowIndex <= 2)
            {
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontSize = rowIndex == 2 ? 14 : 11;
            }
            rowIndex++;
        }

        rowIndex++;
        var headerRow = rowIndex;
        for (var c = 0; c < columnCount; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = columns[c].Header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#D9D9D9");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.WrapText = true;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        }

        var firstDataRow = headerRow + 1;
        foreach (var row in rows)
        {
            rowIndex++;
            for (var c = 0; c < columnCount; c++)
            {
                var value = columns[c].Value(row);
                var cell = sheet.Cell(rowIndex, c + 1);
                cell.Value = ToCellValue(value);
                if (columns[c].Format is { } format)
                {
                    cell.Style.NumberFormat.Format = format;
                }
                widths[c] = Math.Max(widths[c], DisplayLength(value, columns[c].Format));
            }
        }
        var lastDataRow = rowIndex;

        if (columns.Any(c => c.Total || c.FixedTotal.HasValue))
        {
            rowIndex++;
            var labelColumn = columns.TakeWhile(c => !c.Total && !c.FixedTotal.HasValue).Count();
            if (labelColumn > 0)
            {
                sheet.Cell(rowIndex, labelColumn).Value = "Total";
            }

            for (var c = 0; c < columnCount; c++)
            {
                var cell = sheet.Cell(rowIndex, c + 1);
                if (columns[c].FixedTotal is { } fixedTotal)
                {
                    cell.Value = (double)fixedTotal;
                }
                else if (columns[c].Total && lastDataRow >= firstDataRow)
                {
                    // SUBTOTAL(9, ...) suma solo las filas visibles: el total sigue al filtro.
                    var first = sheet.Cell(firstDataRow, c + 1).Address.ToStringRelative();
                    var last = sheet.Cell(lastDataRow, c + 1).Address.ToStringRelative();
                    cell.FormulaA1 = $"SUBTOTAL(9,{first}:{last})";
                }
                else if (columns[c].Total)
                {
                    cell.Value = 0;
                }
                cell.Style.NumberFormat.Format = columns[c].Format ?? MoneyFormat;
            }

            var totalRange = sheet.Range(rowIndex, 1, rowIndex, columnCount);
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#EDEDED");
            totalRange.Style.Border.TopBorder = XLBorderStyleValues.Thin;
        }

        if (lastDataRow >= firstDataRow)
        {
            sheet.Range(headerRow, 1, lastDataRow, columnCount).SetAutoFilter();
        }
        sheet.SheetView.FreezeRows(headerRow);

        // Ancho calculado a mano (no AdjustToContents): no depende de que el servidor tenga fuentes.
        for (var c = 0; c < columnCount; c++)
        {
            sheet.Column(c + 1).Width = Math.Clamp(widths[c] + 2, 8, 60);
        }

        sheet.PageSetup.PageOrientation = columnCount > 7 ? XLPageOrientation.Landscape : XLPageOrientation.Portrait;
        sheet.PageSetup.FitToPages(1, 0);
        sheet.PageSetup.SetRowsToRepeatAtTop(headerRow, headerRow);
    }

    private static XLCellValue ToCellValue(object? value) => value switch
    {
        null => Blank.Value,
        string text => text,
        decimal number => (double)number,
        double number => number,
        long number => number,
        int number => number,
        DateTime date => date,
        _ => value.ToString() ?? string.Empty
    };

    private static double DisplayLength(object? value, string? format) => value switch
    {
        null => 0,
        decimal number => number.ToString("N2", ReportCulture).Length,
        DateTime => 10,
        long or int when format == IdFormat => 5,
        _ => value.ToString()?.Length ?? 0
    };

    private static string PeriodText(object? from, object? to)
    {
        static string Format(object? date) => date is IFormattable formattable ? formattable.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "";

        return (from, to) switch
        {
            (not null, not null) => $"{Format(from)} al {Format(to)}",
            (not null, null) => $"Desde {Format(from)}",
            (null, not null) => $"Hasta {Format(to)}",
            _ => "Todo el historial"
        };
    }

    private async Task<string> WarehouseNameAsync(long? warehouseId)
    {
        if (!warehouseId.HasValue)
        {
            return "Todos los almacenes";
        }
        var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(warehouseId.Value);
        return warehouse?.Name ?? "Todos los almacenes";
    }

    private async Task<string?> ClientNameAsync(long clientId)
    {
        var client = await _unitOfWork.Repository<Client>().GetByIdAsync(clientId);
        if (client is null)
        {
            return null;
        }
        var party = await _unitOfWork.Repository<Party>().GetByIdAsync(client.PartyId);
        return party?.Name;
    }

    private async Task<string> CurrentUsernameAsync()
    {
        if (!_currentUserService.AppUserId.HasValue)
        {
            return "—";
        }
        var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
        return user?.Username ?? "—";
    }
}
