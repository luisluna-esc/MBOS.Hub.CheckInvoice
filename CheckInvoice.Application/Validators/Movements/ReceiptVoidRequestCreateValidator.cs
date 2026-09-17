using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Validators.Movements;

public class ReceiptVoidRequestCreateValidator : AbstractValidator<ReceiptVoidRequestCreateDto>
{
    public ReceiptVoidRequestCreateValidator(IUnitOfWork unitOfWork)
    {
        RuleFor(x => x.ReceiptId)
            .MustAsync((receiptId, ct) => IsVoidableReceiptAsync(unitOfWork, receiptId, ct))
            .WithMessage("This receipt does not exist, is already voided, or already has a pending void request.");

        RuleFor(x => x.VoidReasonId)
            .MustAsync((voidReasonId, ct) => unitOfWork.Repository<VoidReason>().Query()
                .AnyAsync(r => r.VoidReasonId == voidReasonId, ct))
            .WithMessage("VoidReasonId does not reference an existing void reason.");

        RuleFor(x => x.Detail)
            .MaximumLength(255).WithMessage("Detail must not exceed 255 characters.");
    }

    private static async Task<bool> IsVoidableReceiptAsync(IUnitOfWork unitOfWork, long receiptId, CancellationToken ct)
    {
        var receipt = await unitOfWork.Repository<Receipt>().Query()
            .FirstOrDefaultAsync(r => r.ReceiptId == receiptId, ct);

        if (receipt is null || receipt.IsVoided)
        {
            return false;
        }

        return !await unitOfWork.Repository<ReceiptVoidRequest>().Query()
            .AnyAsync(r => r.ReceiptId == receiptId && r.Status == "pending", ct);
    }
}
