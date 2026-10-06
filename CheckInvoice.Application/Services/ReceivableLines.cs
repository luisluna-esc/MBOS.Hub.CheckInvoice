using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

// Productos de las salidas en cuentas por cobrar, con lo pagado y lo que falta de cada uno.
// Cada línea vale su TotalCost (la cuenta por cobrar es la suma de esas líneas) y lo pagado sale
// de payment_detail. Lo usan Caja (al repartir un depósito) y el portal del Pastor (Mi Cuenta).
internal static class ReceivableLines
{
    public static async Task<Dictionary<long, List<PaymentLineDto>>> ForIssuesAsync(IUnitOfWork unitOfWork, IReadOnlyCollection<long> issueIds)
    {
        if (issueIds.Count == 0)
        {
            return [];
        }

        var lines = await (
                from d in unitOfWork.Repository<IssueDetail>().Query()
                join p in unitOfWork.Repository<Product>().Query() on d.ProductId equals p.ProductId
                where issueIds.Contains(d.IssueId)
                orderby d.IssueDetailId
                select new { d.IssueId, Line = new PaymentLineDto
                {
                    IssueDetailId = d.IssueDetailId,
                    ProductId = p.ProductId,
                    Code = p.Code,
                    Name = p.Name,
                    Quantity = d.Quantity,
                    TotalAmount = d.TotalCost
                } })
            .ToListAsync();

        var issueDetailIds = lines.Select(l => l.Line.IssueDetailId).ToList();
        var paidByLine = await unitOfWork.Repository<PaymentDetail>().Query()
            .Where(pd => issueDetailIds.Contains(pd.IssueDetailId))
            .GroupBy(pd => pd.IssueDetailId)
            .Select(g => new { IssueDetailId = g.Key, Paid = g.Sum(pd => pd.Amount) })
            .ToDictionaryAsync(x => x.IssueDetailId, x => x.Paid);

        foreach (var item in lines)
        {
            item.Line.PaidAmount = paidByLine.GetValueOrDefault(item.Line.IssueDetailId);
            item.Line.RemainingAmount = Math.Max(item.Line.TotalAmount - item.Line.PaidAmount, 0);
        }

        return lines.GroupBy(l => l.IssueId).ToDictionary(g => g.Key, g => g.Select(l => l.Line).ToList());
    }
}
