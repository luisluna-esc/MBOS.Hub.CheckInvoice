using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.core.Entities.Security;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

// Hoja de firmas de una salida por lote, como la planilla que ya usaban (ej. "Misioneros
// adultos y niños"): una fila por pastor con distrito, C.I. y la cantidad de cada material, y
// la columna Firma con "Entregado", como en la planilla que firmaban a mano. Los datos salen de
// sp_get_issue_batch_sheet (Scrips/storedProcedures.sql).
public partial class ReportService
{
    // Hasta 6 materiales la hoja va en carta vertical; con más, en carta horizontal para que las
    // columnas no queden demasiado angostas.
    private const int BatchPortraitMaxMaterials = 6;

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
        // Por distrito y, dentro de cada uno, por apellido (los nombres se guardan "Apellidos Nombres").
        var pastors = active
            .GroupBy(l => l.IssueId)
            .Select(g => new
            {
                First = g.First(),
                Name = g.First().ClientName ?? "—",
                Quantities = g.GroupBy(l => l.ProductId).ToDictionary(x => x.Key, x => x.Sum(l => l.Quantity))
            })
            .OrderBy(p => p.First.DistrictName, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase)
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

        var title = $"N° {number}";
        var landscape = products.Count > BatchPortraitMaxMaterials;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                // Carta (vertical u horizontal): las columnas se reparten todo el ancho de la hoja.
                page.Size(landscape ? PageSizes.Letter.Landscape() : PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(9));

                page.Header().Element(c => ReportHeaderWithoutCurrency(c, title, info.ToArray()));

                page.Content().PaddingTop(6).Column(column =>
                {
                    // Lo que se entrega (ej. "4° Trimestre - Misioneros adultos y niños").
                    if (!string.IsNullOrWhiteSpace(head.Description))
                    {
                        column.Item().PaddingBottom(8).AlignCenter().Text(head.Description.ToUpperInvariant())
                            .Bold().FontSize(12).FontColor(ReportAccent);
                    }

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(22);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.6f);
                            columns.ConstantColumn(54);
                            foreach (var _ in products)
                            {
                                columns.RelativeColumn(0.9f);
                            }
                            columns.ConstantColumn(landscape ? 90 : 70);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).AlignCenter().Text("N°");
                            header.Cell().Element(HeaderCell).Text("Distrito");
                            header.Cell().Element(HeaderCell).Text("Pastor");
                            header.Cell().Element(HeaderCell).Text("C.I.");
                            foreach (var product in products)
                            {
                                header.Cell().Element(HeaderCell).AlignCenter().Text(product.ProductName).FontSize(7.5f);
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
                            table.Cell().Element(SignatureRowCell).Text(pastor.Name);
                            table.Cell().Element(SignatureRowCell).Text(pastor.First.TaxId ?? "");
                            foreach (var product in products)
                            {
                                var quantity = pastor.Quantities.GetValueOrDefault(product.ProductId);
                                table.Cell().Element(SignatureRowCell).AlignCenter().Text(quantity > 0 ? Qty(quantity) : "");
                            }
                            table.Cell().Element(SignatureSpaceCell).AlignCenter().AlignMiddle().Text("Entregado");
                        }

                        table.Cell().ColumnSpan(4).Element(CategoryTotalCell).AlignRight().Text("Total").Bold();
                        foreach (var product in products)
                        {
                            table.Cell().Element(CategoryTotalCell).AlignCenter().Text(Qty(totals[product.ProductId])).Bold();
                        }
                        table.Cell().Element(CategoryTotalCell).Text("");
                    });

                    column.Item().PaddingTop(60).Row(signatures =>
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
        container.BorderBottom(0.5f).BorderColor(ReportLine).MinHeight(30).PaddingHorizontal(4).AlignMiddle();

    // Columna Firma: dice "Entregado" (como en la planilla), separada de las cantidades por una
    // línea de todo el alto.
    private static IContainer SignatureSpaceCell(IContainer container) =>
        container.BorderBottom(0.5f).BorderLeft(0.5f).BorderColor(ReportLine).MinHeight(30);
}
