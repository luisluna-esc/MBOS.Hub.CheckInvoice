using System.Globalization;
using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.QueryFilters.Reports;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Informes para contabilidad: el mismo formato del archivo que se importa en el sistema
// contable (hoja "Transaction_Entry", 13 columnas, valores como texto con coma decimal).
// Una fila por producto de cada salida (total en negativo) o de cada entrada (en positivo).
public partial class ReportService
{
    private static readonly string[] AccountingHeaders =
    [
        "AccountCode", "SubAccountCode", "FundCode", "FunctionCode", "RestrictionCode", "EntityValue",
        "SendMemo", "Description", "DueDate", "Memo", "InvoiceNumber", "InvoiceDate", "CurrencyAmount"
    ];

    // Coma decimal y sin separador de miles ("-135,72"). Formato propio en vez de una cultura
    // ("es-BO"), para no depender de que el servidor tenga instaladas las culturas.
    private static readonly NumberFormatInfo AccountingCulture = new()
    {
        NumberDecimalSeparator = ",",
        NumberGroupSeparator = string.Empty,
        NegativeSign = "-"
    };

    public async Task<List<AccountingEntryRowDto>> GetIssuesAccountingReport(AccountingReportQueryFilter filter)
    {
        // Las anuladas no cuentan: sus productos volvieron al almacén.
        await LoadIssueNumbersAsync();
        var issues = _unitOfWork.Repository<Issue>().Query().Where(i => !i.IsVoided);
        if (filter.WarehouseId.HasValue)
        {
            issues = issues.Where(i => i.WarehouseId == filter.WarehouseId.Value);
        }
        // IssueDate es la fecha de la salida (día calendario), no un instante.
        if (filter.DateFrom.HasValue)
        {
            var from = filter.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            issues = issues.Where(i => i.IssueDate >= from);
        }
        if (filter.DateTo.HasValue)
        {
            var toExclusive = filter.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            issues = issues.Where(i => i.IssueDate < toExclusive);
        }

        var lines = await (
            from i in issues
            join d in _unitOfWork.Repository<IssueDetail>().Query() on i.IssueId equals d.IssueId
            join p in _unitOfWork.Repository<Product>().Query() on d.ProductId equals p.ProductId
            join dep in _unitOfWork.Repository<Department>().Query() on p.DepartmentId equals dep.DepartmentId into departments
            from dep in departments.DefaultIfEmpty()
            join w in _unitOfWork.Repository<Warehouse>().Query() on i.WarehouseId equals w.WarehouseId into warehouses
            from w in warehouses.DefaultIfEmpty()
            orderby i.IssueDate, i.IssueId, d.IssueDetailId
            select new
            {
                i.IssueId,
                i.IssueDate,
                WarehouseName = w != null ? w.Name : null,
                DepartmentName = dep != null ? dep.Name : null,
                DepartmentCode = dep != null ? dep.Code : null,
                ProductCode = p.Code,
                ProductName = p.Name,
                p.FundCode,
                p.FunctionCode,
                p.RestrictionCode,
                d.Quantity,
                d.TotalCost
            }).ToListAsync();

        return lines.Select(line => new AccountingEntryRowDto
        {
            AccountCode = AccountingCodes.Account,
            SubAccountCode = line.DepartmentCode ?? string.Empty,
            FundCode = line.FundCode,
            FunctionCode = line.FunctionCode,
            RestrictionCode = line.RestrictionCode,
            EntityValue = -Math.Round(line.TotalCost, 2, MidpointRounding.AwayFromZero),
            SendMemo = AccountingCodes.SendMemo,
            Description = $"{IssueNo(line.IssueId)} - {AccountingQuantity(line.Quantity)} Uds. {line.ProductName}",
            DocumentId = line.IssueId,
            DocumentDate = DateTime.SpecifyKind(line.IssueDate, DateTimeKind.Unspecified),
            WarehouseName = line.WarehouseName,
            DepartmentName = line.DepartmentName,
            ProductCode = line.ProductCode
        }).ToList();
    }

    public async Task<byte[]> GenerateIssuesAccountingExcel(AccountingReportQueryFilter filter) =>
        BuildAccountingWorkbook(await GetIssuesAccountingReport(filter));

    public async Task<List<AccountingEntryRowDto>> GetReceiptsAccountingReport(AccountingReportQueryFilter filter)
    {
        // Las anuladas no cuentan: sus productos ya salieron del inventario.
        var receipts = _unitOfWork.Repository<Receipt>().Query().Where(r => !r.IsVoided);
        if (filter.WarehouseId.HasValue)
        {
            receipts = receipts.Where(r => r.WarehouseId == filter.WarehouseId.Value);
        }
        if (filter.ReceiptTypeId.HasValue)
        {
            receipts = receipts.Where(r => r.ReceiptTypeId == filter.ReceiptTypeId.Value);
        }
        // IssueDate es la fecha de ingreso al almacén (día calendario).
        if (filter.DateFrom.HasValue)
        {
            var from = filter.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            receipts = receipts.Where(r => r.IssueDate >= from);
        }
        if (filter.DateTo.HasValue)
        {
            var toExclusive = filter.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            receipts = receipts.Where(r => r.IssueDate < toExclusive);
        }

        var lines = await (
            from r in receipts
            join d in _unitOfWork.Repository<ReceiptDetail>().Query() on r.ReceiptId equals d.ReceiptId
            join p in _unitOfWork.Repository<Product>().Query() on d.ProductId equals p.ProductId
            join dep in _unitOfWork.Repository<Department>().Query() on p.DepartmentId equals dep.DepartmentId into departments
            from dep in departments.DefaultIfEmpty()
            join w in _unitOfWork.Repository<Warehouse>().Query() on r.WarehouseId equals w.WarehouseId into warehouses
            from w in warehouses.DefaultIfEmpty()
            join t in _unitOfWork.Repository<ReceiptType>().Query() on r.ReceiptTypeId equals t.ReceiptTypeId into types
            from t in types.DefaultIfEmpty()
            orderby r.IssueDate, r.MonthlyNumber, r.ReceiptId, d.ReceiptDetailId
            select new
            {
                r.ReceiptId,
                r.MonthlyNumber,
                r.IssueDate,
                r.InvoiceNumber,
                TypeName = t != null ? t.Name : null,
                WarehouseName = w != null ? w.Name : null,
                DepartmentName = dep != null ? dep.Name : null,
                DepartmentCode = dep != null ? dep.Code : null,
                ProductCode = p.Code,
                ProductName = p.Name,
                p.FundCode,
                p.FunctionCode,
                p.RestrictionCode,
                d.Quantity,
                d.TotalCost
            }).ToListAsync();

        return lines.Select(line =>
        {
            var note = ReceiptNumbering.Format(line.MonthlyNumber, line.IssueDate, line.ReceiptId);
            // Sin el N° de nota (va en su propia columna): la factura, si la hay, y "58 Uds. Producto".
            var invoice = string.IsNullOrWhiteSpace(line.InvoiceNumber) || line.InvoiceNumber.Trim() == "0"
                ? string.Empty
                : $"Fact. {line.InvoiceNumber.Trim()} - ";
            return new AccountingEntryRowDto
            {
                AccountCode = AccountingCodes.Account,
                SubAccountCode = line.DepartmentCode ?? string.Empty,
                FundCode = line.FundCode,
                FunctionCode = line.FunctionCode,
                RestrictionCode = line.RestrictionCode,
                EntityValue = Math.Round(line.TotalCost, 2, MidpointRounding.AwayFromZero),
                SendMemo = AccountingCodes.SendMemo,
                Description = $"{invoice}{AccountingQuantity(line.Quantity)} Uds. {line.ProductName}",
                DocumentId = line.ReceiptId,
                DocumentDate = DateTime.SpecifyKind(line.IssueDate, DateTimeKind.Unspecified),
                DocumentNumber = note,
                DocumentType = line.TypeName,
                WarehouseName = line.WarehouseName,
                DepartmentName = line.DepartmentName,
                ProductCode = line.ProductCode
            };
        }).ToList();
    }

    public async Task<byte[]> GenerateReceiptsAccountingExcel(AccountingReportQueryFilter filter) =>
        BuildAccountingWorkbook(await GetReceiptsAccountingReport(filter));

    /// <summary>"58" o "2,5": la cantidad como en el archivo de ejemplo (sin decimales si es entera).</summary>
    private static string AccountingQuantity(decimal quantity) =>
        quantity == decimal.Truncate(quantity)
            ? decimal.Truncate(quantity).ToString(CultureInfo.InvariantCulture)
            : quantity.ToString("0.##", AccountingCulture);

    private static byte[] BuildAccountingWorkbook(List<AccountingEntryRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Transaction_Entry");

        for (var c = 0; c < AccountingHeaders.Length; c++)
        {
            sheet.Cell(1, c + 1).Value = AccountingHeaders[c];
        }

        var rowIndex = 1;
        foreach (var row in rows)
        {
            rowIndex++;
            // Todo como texto, igual que el archivo que acepta el sistema contable
            // (el valor con coma decimal y sin separador de miles: "-135,72").
            string[] values =
            [
                row.AccountCode, row.SubAccountCode, row.FundCode, row.FunctionCode, row.RestrictionCode,
                row.EntityValue.ToString("0.00", AccountingCulture), row.SendMemo, row.Description
            ];
            for (var c = 0; c < values.Length; c++)
            {
                var cell = sheet.Cell(rowIndex, c + 1);
                cell.Style.NumberFormat.Format = "@";
                cell.Value = values[c];
            }
        }

        sheet.Column(8).Width = 60;
        for (var c = 1; c <= AccountingHeaders.Length; c++)
        {
            if (c != 8)
            {
                sheet.Column(c).Width = Math.Max(12, AccountingHeaders[c - 1].Length + 2);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
