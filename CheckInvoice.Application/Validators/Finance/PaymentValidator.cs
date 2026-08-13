using CheckInvoice.Application.Dtos.Finance;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Finance;

public class PaymentValidator : AbstractValidator<PaymentDto>
{
    public PaymentValidator()
    {
        RuleFor(x => x.AccountReceivableId)
            .GreaterThan(0).WithMessage("AccountReceivableId is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.PaymentMethod)
            .MaximumLength(30).WithMessage("PaymentMethod must not exceed 30 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(255).WithMessage("Notes must not exceed 255 characters.");
    }
}