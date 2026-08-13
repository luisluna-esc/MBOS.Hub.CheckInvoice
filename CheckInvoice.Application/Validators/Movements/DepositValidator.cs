using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class DepositValidator : AbstractValidator<DepositDto>
{
    public DepositValidator()
    {
        RuleFor(x => x.ReceiptNumber)
            .MaximumLength(20).WithMessage("ReceiptNumber must not exceed 20 characters.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Notes)
            .MaximumLength(255).WithMessage("Notes must not exceed 255 characters.");
    }
}