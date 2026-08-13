using CheckInvoice.Application.Dtos.Finance;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Finance;

public class InstallmentValidator : AbstractValidator<InstallmentDto>
{
    public InstallmentValidator()
    {
        RuleFor(x => x.AccountReceivableId)
            .GreaterThan(0).WithMessage("AccountReceivableId is required.");

        RuleFor(x => x.InstallmentNumber)
            .GreaterThan(0).WithMessage("InstallmentNumber must be greater than zero.");

        RuleFor(x => x.InstallmentAmount)
            .GreaterThan(0).WithMessage("InstallmentAmount must be greater than zero.");
    }
}