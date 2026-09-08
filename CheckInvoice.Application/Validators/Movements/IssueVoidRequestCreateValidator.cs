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
            .WithMessage("This issue does not exist, is already voided, or already has a pending void request.");

        RuleFor(x => x.VoidReasonId)
            .MustAsync((voidReasonId, ct) => unitOfWork.Repository<VoidReason>().Query()
                .AnyAsync(r => r.VoidReasonId == voidReasonId, ct))
            .WithMessage("VoidReasonId does not reference an existing void reason.");

        RuleFor(x => x.Detail)
            .MaximumLength(255).WithMessage("Detail must not exceed 255 characters.");
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
}
