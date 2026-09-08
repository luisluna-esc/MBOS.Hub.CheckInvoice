using CheckInvoice.Application.Dtos.Finance;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Finance;

public class AccountReceivableValidator : AbstractValidator<AccountReceivableDto>
{
    public AccountReceivableValidator()
    {
        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("TotalAmount must be greater than zero.");

        RuleFor(x => x.PaymentType)
            .NotEmpty().WithMessage("PaymentType is required.")
            .MaximumLength(20).WithMessage("PaymentType must not exceed 20 characters.");

        RuleFor(x => x.PaymentDetail)
            .MaximumLength(255).WithMessage("PaymentDetail must not exceed 255 characters.");
    }
}