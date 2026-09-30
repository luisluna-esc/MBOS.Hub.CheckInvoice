using CheckInvoice.Application.Dtos.Finance;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Finance;

public class InstallmentValidator : AbstractValidator<InstallmentDto>
{
    public InstallmentValidator()
    {
        RuleFor(x => x.AccountReceivableId)
            .GreaterThan(0).WithMessage("Debes indicar la cuenta por cobrar.");

        RuleFor(x => x.InstallmentNumber)
            .GreaterThan(0).WithMessage("El número de cuota debe ser mayor a cero.");

        RuleFor(x => x.InstallmentAmount)
            .GreaterThan(0).WithMessage("El monto de la cuota debe ser mayor a cero.");
    }
}
