using CheckInvoice.Application.Dtos.Reports;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.QueryFilters.Reports;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CheckInvoice.Application.Services;

// Reporte de Transferencias entre almacenes propios: una fila por producto transferido,
// agrupado por transferencia. Como el comprobante: cantidad primero, sin código y con costos.
public partial class ReportService
{
    public async Task<List<TransfersReportRowDto>> GetTransfersReport(TransfersReportQueryFilter filter)
    {
        // Las anuladas no cuentan: sus productos volvieron al origen.
        var transfers = _unitOfWork.Repository<Transfer>().Query().Where(t => !t.IsVoided);
        if (filter.SourceWarehouseId.HasValue)
        {
            transfers = transfers.Where(t => t.SourceWarehouseId == filter.SourceWarehouseId.Value);
        }
        if (filter.DestinationWarehouseId.HasValue)
        {
            transfers = transfers.Where(t => t.DestinationWarehouseId == filter.DestinationWarehouseId.Value);
        }
        // Firma: actualizado = firmado (verificado o aprobado) o anterior a la firma digital.
        if (filter.ReviewState == ReviewStates.Updated)
        {
            transfers = transfers.Where(t => ReviewStates.UpdatedStatuses.Contains(t.ReviewStatus));
        }
        else if (filter.ReviewState == ReviewStates.Outdated)
        {
            transfers = transfers.Where(t => !ReviewStates.UpdatedStatuses.Contains(t.ReviewStatus));
        }

        // TransferDate es un instante en UTC: el rango de días se arma en hora de Bolivia (UTC-4).
        if (filter.DateFrom.HasValue)
        {
            var fromUtc = filter.DateFrom.Value.ToDateTime(TimeOnly.MinValue).AddHours(4);
            transfers = transfers.Where(t => t.TransferDate >= fromUtc);
        }
        if (filter.DateTo.HasValue)
        {
            var toUtc = filter.DateTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(4);
            transfers = transfers.Where(t => t.TransferDate < toUtc);
        }

        var rows = await (
            from t in transfers
            join d in _unitOfWork.Repository<TransferDetail>().Query() on t.TransferId equals d.TransferId
            join p in _unitOfWork.Repository<Product>().Query() on d.ProductId equals p.ProductId
            join m in _unitOfWork.Repository<MediaType>().Query() on p.MediaTypeId equals m.MediaTypeId into media
            from m in media.DefaultIfEmpty()
            join s in _unitOfWork.Repository<Warehouse>().Query() on t.SourceWarehouseId equals s.WarehouseId into sources
            from s in sources.DefaultIfEmpty()
            join w in _unitOfWork.Repository<Warehouse>().Query() on t.DestinationWarehouseId equals w.WarehouseId into destinations
            from w in destinations.DefaultIfEmpty()
            join u in _unitOfWork.Repository<AppUser>().Query() on t.SenderUserId equals u.AppUserId into users
            from u in users.DefaultIfEmpty()
            orderby t.TransferDate, t.TransferId, d.TransferDetailId
            select new TransfersReportRowDto
            {
                TransferId = t.TransferId,
                TransferDate = t.TransferDate,
                SourceWarehouseName = s != null ? s.Name : null,
                DestinationWarehouseName = w != null ? w.Name : null,
                SenderName = u != null ? u.FirstName + " " + u.LastName : null,
                ReviewStatus = t.ReviewStatus,
                Name = p.Name,
                UnitMeasure = m != null ? m.Name : null,
                Quantity = d.Quantity,
                UnitCost = d.UnitPrice ?? 0,
                TotalCost = d.TotalSalePrice ?? 0
            }).ToListAsync();

        foreach (var row in rows)
        {
            row.TransferDate = BoliviaTime.FromUtc(row.TransferDate);
        }
        return rows;
    }

    private async Task<List<string>> TransfersReportInfoAsync(TransfersReportQueryFilter filter) =>
    [
        $"Origen: {await WarehouseNameAsync(filter.SourceWarehouseId)}",
        $"Destino: {await WarehouseNameAsync(filter.DestinationWarehouseId)}",
        $"Periodo: {PeriodText(filter.DateFrom, filter.DateTo)}",
        $"Estado: {ReviewStates.Label(filter.ReviewState)}"
    ];

    public async Task<byte[]> GenerateTransfersReportPdf(TransfersReportQueryFilter filter)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        var rows = await GetTransfersReport(filter);
        var info = await TransfersReportInfoAsync(filter);

        var username = "—";
        if (_currentUserService.AppUserId.HasValue)
        {
            var user = await _unitOfWork.Repository<AppUser>().GetByIdAsync(_currentUserService.AppUserId.Value);
            username = user?.Username ?? username;
        }

        var groups = rows.GroupBy(r => r.TransferId).ToList();
        var grandQuantity = rows.Sum(r => r.Quantity);
        var grandTotal = rows.Sum(r => r.TotalCost);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontColor(ReportInk).FontSize(7.5f));

                page.Header().Element(c => ReportHeader(c, "Transferencias de Almacén", info.ToArray()));

                page.Content().PaddingTop(10).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(50);
                        columns.ConstantColumn(55);
                        columns.RelativeColumn(1);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(60);
                        columns.ConstantColumn(100);
                    });

                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).AlignRight().Text("Cantidad");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Unidad");
                        header.Cell().Element(HeaderCell).Text("Descripción");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nUnitario");
                        header.Cell().Element(HeaderCell).AlignRight().Text("Costo\nTotal");
                        header.Cell().Element(HeaderCell).AlignCenter().Text("Revisión");
                    });

                    foreach (var group in groups)
                    {
                        var first = group.First();
                        table.Cell().ColumnSpan(6).Element(GroupTitleCell).Text(t =>
                        {
                            t.Span($"Transferencia N° {first.TransferId.ToString().PadLeft(5, '0')}").Bold().FontSize(9);
                            t.Span($"  ·  {first.TransferDate:dd/MM/yyyy}  ·  {first.SourceWarehouseName ?? "—"} → {first.DestinationWarehouseName ?? "—"}  ·  Envía: {first.SenderName ?? "—"}").FontSize(8);
                        });

                        foreach (var row in group)
                        {
                            table.Cell().Element(BodyCell).AlignRight().Text(Qty(row.Quantity));
                            table.Cell().Element(BodyCell).AlignCenter().Text(row.UnitMeasure ?? "");
                            table.Cell().Element(BodyCell).Text(row.Name);
                            table.Cell().Element(BodyCell).AlignRight().Text(row.UnitCost.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignRight().Text(row.TotalCost.ToString("N2", ReportCulture));
                            table.Cell().Element(BodyCell).AlignCenter().Text(DocumentReviews.Label(row.ReviewStatus));
                        }

                        table.Cell().Element(SubtotalCell).AlignRight().Text(Qty(group.Sum(r => r.Quantity))).Bold();
                        table.Cell().ColumnSpan(3).Element(SubtotalCell).AlignRight().Text("Total transferencia").Bold();
                        table.Cell().Element(SubtotalCell).AlignRight().Text(group.Sum(r => r.TotalCost).ToString("N2", ReportCulture)).Bold();
                        table.Cell().Element(SubtotalCell).Text("");
                    }

                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(Qty(grandQuantity)).Bold();
                    table.Cell().ColumnSpan(3).Element(CategoryTotalCell).AlignRight().Text($"TOTAL GENERAL ({groups.Count} transferencias)").Bold();
                    table.Cell().Element(CategoryTotalCell).AlignRight().Text(grandTotal.ToString("N2", ReportCulture)).Bold();
                    table.Cell().Element(CategoryTotalCell).Text("");
                });

                page.Footer().Element(c => ReportFooter(c, username));
            });
        });

        return WithTitle(document, "Transferencias de Almacén").GeneratePdf();
    }

    public async Task<byte[]> GenerateTransfersReportExcel(TransfersReportQueryFilter filter)
    {
        var rows = await GetTransfersReport(filter);
        var info = await TransfersReportInfoAsync(filter);
        var columns = new List<ExcelColumn<TransfersReportRowDto>>
        {
            new("N° Transferencia", r => r.TransferId, IdFormat),
            new("Fecha", r => r.TransferDate.Date, DateFormat),
            new("Origen", r => r.SourceWarehouseName),
            new("Destino", r => r.DestinationWarehouseName),
            new("Envía", r => r.SenderName),
            new("Cantidad", r => r.Quantity, QuantityFormat, Total: true),
            new("Unidad", r => r.UnitMeasure),
            new("Descripción", r => r.Name),
            new("Costo unitario", r => r.UnitCost, MoneyFormat),
            new("Costo total", r => r.TotalCost, MoneyFormat, Total: true),
            new("Revisión", r => DocumentReviews.Label(r.ReviewStatus))
        };
        return await BuildWorkbookAsync(wb => AddSheet(wb, "Transferencias", "TRANSFERENCIAS DE ALMACÉN", info, columns, rows));
    }
}
