using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.core.Entities.Security;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

// Hoja de firmas de una salida por lote, como la planilla que ya usaban (ej. "Misioneros
// adultos y niños"): una fila por pastor con distrito, C.I. y la cantidad de cada material, y
// la columna Firma en blanco porque firman a mano al recibir. Los datos salen de
// sp_get_issue_batch_sheet (Scrips/storedProcedures.sql).
public partial class ReportService
{
    // Hasta 4 materiales la hoja va vertical; con más, horizontal para que quepan las columnas.
    private const int BatchPortraitMaxMaterials = 4;

    private const string GetIssueBatchSheetSql = """
        SELECT
            issue_batch_id AS "IssueBatchId",
            monthly_number AS "MonthlyNumber",
            batch_date AS "BatchDate",
            warehouse_name AS "WarehouseName",
            description AS "Description",
            created_by_name AS "CreatedByName",
            issue_id AS "IssueId",
            is_voided AS "IsVoided",
            client_name AS "ClientName",
            tax_id AS "TaxId",
            district_name AS "DistrictName",
            product_id AS "ProductId",
            product_code AS "ProductCode",
            product_name AS "ProductName",
            department_name AS "DepartmentName",
            quantity AS "Quantity"
        FROM sp_get_issue_batch_sheet({0}::bigint)
        """;

    public async Task<byte[]?> GenerateIssueBatchPdf(long issueBatchId)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var lines = await _unitOfWork.SqlQueryAsync<IssueBatchSheetRowDto>(GetIssueBatchSheetSql, issueBatchId);
        if (lines.Count == 0)
        {
            return null;
        }

        var head = lines[0];
        var number = IssueNumbering.Format(head.MonthlyNumber, head.BatchDate, head.IssueBatchId);
        // Las anuladas no se entregaron: no van en la hoja.
        var active = lines.Where(l => !l.IsVoided).ToList();
        var products = active
            .GroupBy(l => l.ProductId)
            .Select(g => g.First())
            .OrderBy(p => p.ProductName)
            .ToList();
        // El SP ya viene ordenado por distrito y pastor.
        var pastors = active
            .GroupBy(l => l.IssueId)
            .Select(g => new
            {
                First = g.First(),
                Quantities = g.GroupBy(l => l.ProductId).ToDictionary(x => x.Key, x => x.Sum(l => l.Quantity))
            })
            .ToList();
        var totals = products.ToDictionary(p => p.ProductId, p => active.Where(l => l.ProductId == p.ProductId).Sum(l => l.Quantity));

        var departments = products.Select(p => p.DepartmentName).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct().ToList();
        var info = new List<string?>
        {
            $"Fecha: {head.BatchDate:dd/MM/yyyy}",
            $"Almacén: {head.WarehouseName ?? "—"}",
            departments.Count > 0 ? $"Departamento: {string.Join(", ", departments)}" : null,
            $"Pastores: {pastors.Count}"
        };

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var landscape = products.Count > BatchPortraitMaxMaterials;
        var title = $"N° {number}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(landscape ? PageSizes.Letter.Landscape() : PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(7.5f));

                page.Header().Element(c => ReportHeaderWithoutCurrency(c, title, info.ToArray()));

                page.Content().PaddingTop(6).Column(column =>
                {
                    // Lo que se entrega (ej. "4° Trimestre - Misioneros adultos y niños").
                    if (!string.IsNullOrWhiteSpace(head.Description))
                    {
                        column.Item().PaddingBottom(6).AlignCenter().Text(head.Description.ToUpperInvariant())
                            .Bold().FontSize(10).FontColor(ReportAccent);
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(22);
                            columns.RelativeColumn(1.1f);
                            columns.ConstantColumn(52);
                            columns.RelativeColumn(1.6f);
                            foreach (var _ in products)
                            {
                                columns.ConstantColumn(46);
                            }
                            columns.ConstantColumn(landscape ? 110 : 90);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).AlignCenter().Text("N°");
                            header.Cell().Element(HeaderCell).Text("Distrito");
                            header.Cell().Element(HeaderCell).Text("C.I.");
                            header.Cell().Element(HeaderCell).Text("Pastor");
                            foreach (var product in products)
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text(product.ProductName).FontSize(6.5f);
                            }
                            header.Cell().Element(HeaderCell).AlignCenter().Text("Firma");
                        });

                        var row = 0;
                        foreach (var pastor in pastors)
                        {
                            row++;
                            // Filas altas: la firma va a mano en la última columna.
                            table.Cell().Element(SignatureRowCell).AlignCenter().Text(row.ToString());
                            table.Cell().Element(SignatureRowCell).Text(pastor.First.DistrictName ?? "—");
                            table.Cell().Element(SignatureRowCell).Text(pastor.First.TaxId ?? "");
                            table.Cell().Element(SignatureRowCell).Text(pastor.First.ClientName ?? "—");
                            foreach (var product in products)
                            {
                                var quantity = pastor.Quantities.GetValueOrDefault(product.ProductId);
                                table.Cell().Element(SignatureRowCell).AlignCenter().Text(quantity > 0 ? Qty(quantity) : "");
                            }
                            table.Cell().Element(SignatureSpaceCell);
                        }

                        table.Cell().ColumnSpan(4).Element(CategoryTotalCell).AlignRight().Text("Total").Bold();
                        foreach (var product in products)
                        {
                            table.Cell().Element(CategoryTotalCell).AlignCenter().Text(Qty(totals[product.ProductId])).Bold();
                        }
                        table.Cell().Element(CategoryTotalCell).Text("");
                    });

                    column.Item().PaddingTop(40).Row(signatures =>
                    {
                        signatures.RelativeItem().PaddingHorizontal(30).Column(c => SignatureBox(c, $"Entregado por: {head.CreatedByName ?? ""}"));
                        signatures.RelativeItem().PaddingHorizontal(30).Column(c => SignatureBox(c, "Vo. Bo. Departamento"));
                    });
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, title).GeneratePdf();
    }

    private static IContainer SignatureRowCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderColor(ReportLine).MinHeight(24).PaddingHorizontal(3).AlignMiddle();

    // Espacio en blanco para la firma, separado de las cantidades por una línea de todo el alto.
    private static IContainer SignatureSpaceCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderLeft(0.5f).BorderColor(ReportLine).MinHeight(24);
}
