using System.Globalization;
using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Inventario inicial: se carga una sola vez por almacén desde un Excel (CODIGO, NOMBRE,
// CANTIDAD, COSTO_UNITARIO). Primero se revisa (sin guardar nada) y luego se confirma; al
// confirmar se registra como una Entrada de tipo "Inventario Inicial" con su N° de nota.
public partial class ReceiptService
{
    private const string TemplateSheet = "Inventario inicial";

    public async Task<byte[]> GetInitialInventoryTemplate()
    {
        var products = await _unitOfWork.Repository<Product>().Query()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new { p.Code, p.Name })
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(TemplateSheet);
        string[] headers = ["CODIGO", "NOMBRE (solo referencia)", "CANTIDAD", "COSTO_UNITARIO"];
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
            sheet.Cell(1, i + 1).Style.Fill.BackgroundColor = XLColor.LightGray;
        }

        var row = 2;
        foreach (var product in products)
        {
            sheet.Cell(row, 1).Value = product.Code ?? string.Empty;
            sheet.Cell(row, 2).Value = product.Name;
            row++;
        }

        sheet.Column(3).Style.NumberFormat.Format = "#,##0.00";
        sheet.Column(4).Style.NumberFormat.Format = "#,##0.00";
        sheet.Columns().AdjustToContents();
        sheet.SheetView.FreezeRows(1);

        var help = workbook.Worksheets.Add("Instrucciones");
        help.Cell(1, 1).Value = "Llena CANTIDAD y COSTO_UNITARIO de los productos que hay en el almacén.";
        help.Cell(2, 1).Value = "Las filas sin CANTIDAD se ignoran. El NOMBRE es solo para ubicarse: se busca por CODIGO.";
        help.Cell(3, 1).Value = "Un producto sin código en el sistema no se puede cargar: asígnale un código primero.";
        help.Cell(4, 1).Value = "El inventario inicial se carga una sola vez por almacén.";
        help.Column(1).AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<ResponseGetObject> ImportInitialInventory(Stream excel, long warehouseId, bool confirm)
    {
        var result = new InitialInventoryImportDto();

        var warehouse = await _unitOfWork.Repository<Warehouse>().GetByIdAsync(warehouseId);
        if (warehouse is null || !warehouse.IsActive)
        {
            result.Errors.Add("Elige un almacén activo.");
            return ImportResponse(result, HttpStatusCode.BadRequest);
        }

        var initialType = await _unitOfWork.Repository<ReceiptType>().Query()
            .FirstOrDefaultAsync(t => t.Name == ReceiptCosting.InitialInventory);
        if (initialType is null)
        {
            result.Errors.Add("Falta el tipo de entrada \"Inventario Inicial\" en el catálogo.");
            return ImportResponse(result, HttpStatusCode.BadRequest);
        }

        var previous = await _unitOfWork.Repository<Receipt>().Query()
            .Where(r => r.WarehouseId == warehouseId && r.ReceiptTypeId == initialType.ReceiptTypeId && !r.IsVoided)
            .Select(r => new { r.ReceiptId, r.MonthlyNumber, r.IssueDate })
            .FirstOrDefaultAsync();
        if (previous is not null)
        {
            result.Errors.Add($"El inventario inicial de {warehouse.Name} ya se cargó (nota {ReceiptNumbering.Format(previous.MonthlyNumber, previous.IssueDate, previous.ReceiptId)}). Para corregirlo, anula esa nota primero.");
            return ImportResponse(result, HttpStatusCode.BadRequest);
        }

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(excel);
        }
        catch (Exception)
        {
            result.Errors.Add("El archivo no es un Excel válido (.xlsx).");
            return ImportResponse(result, HttpStatusCode.BadRequest);
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name == TemplateSheet) ?? workbook.Worksheet(1);
            var productsByCode = await _unitOfWork.Repository<Product>().Query()
                .Where(p => p.Code != null)
                .Select(p => new { p.ProductId, p.Code, p.Name })
                .ToListAsync();
            var lookup = productsByCode
                .GroupBy(p => p.Code!.Trim().ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            // Las columnas se buscan por su título (en cualquier orden), así sirve también un Excel
            // propio del almacén y no solo la plantilla. Sin títulos reconocibles, el orden de la
            // plantilla: CODIGO, NOMBRE, CANTIDAD, COSTO_UNITARIO.
            var (headerRow, codeColumn, quantityColumn, costColumn) = FindColumns(sheet);
            if (codeColumn == 0 || quantityColumn == 0 || costColumn == 0)
            {
                result.Errors.Add("No se encontraron las columnas CODIGO, CANTIDAD y COSTO_UNITARIO (o COSTO). Usa la plantilla o pon esos títulos en la primera fila.");
                return ImportResponse(result, HttpStatusCode.BadRequest);
            }

            var seen = new HashSet<long>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
            for (var rowNumber = headerRow + 1; rowNumber <= lastRow; rowNumber++)
            {
                var row = sheet.Row(rowNumber);
                var code = row.Cell(codeColumn).GetString().Trim();
                var quantityCell = row.Cell(quantityColumn);
                var costCell = row.Cell(costColumn);
                if (quantityCell.IsEmpty() && costCell.IsEmpty())
                {
                    continue;
                }

                var label = $"Fila {rowNumber}";
                if (string.IsNullOrEmpty(code))
                {
                    result.Errors.Add($"{label}: falta el CODIGO.");
                    continue;
                }
                if (!lookup.TryGetValue(code.ToUpperInvariant(), out var product))
                {
                    result.Errors.Add($"{label}: no existe un producto con código {code}.");
                    continue;
                }
                if (!seen.Add(product.ProductId))
                {
                    result.Errors.Add($"{label}: el código {code} está repetido.");
                    continue;
                }
                if (!TryReadNumber(quantityCell, out var quantity) || quantity <= 0)
                {
                    result.Errors.Add($"{label} ({code}): la CANTIDAD debe ser un número mayor a cero.");
                    continue;
                }
                if (!TryReadNumber(costCell, out var unitCost) || unitCost < 0)
                {
                    result.Errors.Add($"{label} ({code}): el COSTO_UNITARIO debe ser un número (0 o más).");
                    continue;
                }

                result.Lines.Add(new InitialInventoryLineDto
                {
                    ProductId = product.ProductId,
                    Code = product.Code!,
                    Name = product.Name,
                    Quantity = Math.Round(quantity, 2),
                    UnitCost = Math.Round(unitCost, 2)
                });
            }
        }

        if (result.Lines.Count == 0 && result.Errors.Count == 0)
        {
            result.Errors.Add("El archivo no tiene filas con CANTIDAD.");
        }

        result.TotalQuantity = result.Lines.Sum(l => l.Quantity);
        result.TotalCost = result.Lines.Sum(l => l.Quantity * l.UnitCost);

        if (!confirm || result.Errors.Count > 0)
        {
            return ImportResponse(result, result.Errors.Count > 0 ? HttpStatusCode.BadRequest : HttpStatusCode.OK);
        }

        var insert = await InsertReceipt(new ReceiptRequestDto
        {
            WarehouseId = warehouseId,
            ReceiptTypeId = initialType.ReceiptTypeId,
            Description = "Inventario inicial cargado desde Excel",
            IssueDate = BoliviaTime.Today,
            Details = result.Lines.Select(l => new ReceiptDetailRequestDto
            {
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitCost
            }).ToList()
        });

        if (insert.StatusCode != HttpStatusCode.Created)
        {
            result.Errors.AddRange(insert.Messages.Select(m => m.Description));
            return ImportResponse(result, insert.StatusCode);
        }

        var saved = await _unitOfWork.Repository<Receipt>().GetByIdAsync(insert.Id);
        result.ReceiptId = insert.Id;
        result.NoteNumber = saved is null ? null : ReceiptNumbering.Format(saved.MonthlyNumber, saved.IssueDate, saved.ReceiptId);
        return ImportResponse(result, HttpStatusCode.Created);
    }

    private static string Normalize(string text)
    {
        var cleaned = text.Trim().ToUpperInvariant()
            .Replace('Á', 'A').Replace('É', 'E').Replace('Í', 'I').Replace('Ó', 'O').Replace('Ú', 'U')
            .Replace('°', ' ').Replace('.', ' ').Replace('_', ' ');
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    // Busca la fila de títulos en las primeras 10 filas y reconoce sus columnas por nombre.
    private static (int HeaderRow, int Code, int Quantity, int Cost) FindColumns(IXLWorksheet sheet)
    {
        string[] codeNames = ["CODIGO", "COD", "CODIGO PRODUCTO", "CODE"];
        string[] quantityNames = ["CANTIDAD", "CANT", "STOCK", "EXISTENCIA", "SALDO", "QUANTITY"];
        string[] costNames = ["COSTO UNITARIO", "COSTO", "COSTO UNIT", "PRECIO UNITARIO", "VALOR UNITARIO", "UNIT COST"];

        for (var rowNumber = 1; rowNumber <= Math.Min(10, sheet.LastRowUsed()?.RowNumber() ?? 1); rowNumber++)
        {
            int code = 0, quantity = 0, cost = 0;
            foreach (var cell in sheet.Row(rowNumber).CellsUsed())
            {
                var title = Normalize(cell.GetString());
                if (code == 0 && codeNames.Contains(title))
                {
                    code = cell.Address.ColumnNumber;
                }
                else if (quantity == 0 && quantityNames.Contains(title))
                {
                    quantity = cell.Address.ColumnNumber;
                }
                else if (cost == 0 && costNames.Contains(title))
                {
                    cost = cell.Address.ColumnNumber;
                }
            }
            if (code > 0 && quantity > 0 && cost > 0)
            {
                return (rowNumber, code, quantity, cost);
            }
        }
        return (0, 0, 0, 0);
    }

    private static bool TryReadNumber(IXLCell cell, out decimal value)
    {
        value = 0;
        if (cell.DataType == XLDataType.Number)
        {
            value = (decimal)cell.GetDouble();
            return true;
        }
        var text = cell.GetString().Trim().Replace(" ", string.Empty);
        if (text.Length == 0)
        {
            return false;
        }
        // Acepta "1.234,50" (Bolivia) y "1234.50".
        if (text.Contains(',') && text.Contains('.'))
        {
            text = text.Replace(".", string.Empty);
        }
        text = text.Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static ResponseGetObject ImportResponse(InitialInventoryImportDto result, HttpStatusCode statusCode) => new()
    {
        Data = result,
        Messages = result.Errors.Select(e => new Message { Type = MessageType.Error, Description = e }).ToArray(),
        StatusCode = statusCode
    };
}
