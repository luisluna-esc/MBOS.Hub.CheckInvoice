using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Validators.Movements;

public class IssueVoidRequestCreateValidator : AbstractValidator<IssueVoidRequestCreateDto>
{
    public IssueVoidRequestCreateValidator(IUnitOfWork unitOfWork)
    {
        RuleFor(x => x.IssueId)
            .MustAsync((issueId, ct) => IsVoidableIssueAsync(unitOfWork, issueId, ct))
            .WithMessage("Esta salida no existe, ya fue anulada o ya tiene una solicitud de anulación pendiente.");

        // Anular restaura el 100% del stock original de la Salida; si ya se registró una
        // Devolución parcial contra ella (Receipt.RelatedIssueId), anular duplicaría ese
        // stock ya devuelto. Se bloquea en vez de intentar calcular el neto.
        RuleFor(x => x.IssueId)
            .MustAsync((issueId, ct) => HasNoReturnsAsync(unitOfWork, issueId, ct))
            .WithMessage("Esta salida ya tiene una devolución registrada y no se puede anular.");

        RuleFor(x => x.VoidReasonId)
            .MustAsync((voidReasonId, ct) => unitOfWork.Repository<VoidReason>().Query()
                .AnyAsync(r => r.VoidReasonId == voidReasonId, ct))
            .WithMessage("El motivo de anulación seleccionado no existe.");

        RuleFor(x => x.Detail)
            .MaximumLength(255).WithMessage("El detalle no puede superar los 255 caracteres.");
    }

    private static async Task<bool> IsVoidableIssueAsync(IUnitOfWork unitOfWork, long issueId, CancellationToken ct)
    {
        var issue = await unitOfWork.Repository<Issue>().Query()
            .FirstOrDefaultAsync(i => i.IssueId == issueId, ct);

        if (issue is null || issue.IsVoided)
        {
            return false;
        }

        return !await unitOfWork.Repository<IssueVoidRequest>().Query()
            .AnyAsync(r => r.IssueId == issueId && r.Status == "pending", ct);
    }

    private static async Task<bool> HasNoReturnsAsync(IUnitOfWork unitOfWork, long issueId, CancellationToken ct)
    {
        return !await unitOfWork.Repository<Receipt>().Query()
            .AnyAsync(r => r.RelatedIssueId == issueId && !r.IsVoided, ct);
    }
}
