using CheckInvoice.Application.Dtos.Finance;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Finance;

public class AccountReceivableValidator : AbstractValidator<AccountReceivableDto>
{
    public AccountReceivableValidator()
    {
        RuleFor(x => x.TotalAmount)
            .GreaterThan(0).WithMessage("El monto total debe ser mayor a cero.");

        RuleFor(x => x.PaymentType)
            .NotEmpty().WithMessage("El tipo de pago es obligatorio.")
            .MaximumLength(20).WithMessage("El tipo de pago no puede superar los 20 caracteres.");

        RuleFor(x => x.PaymentDetail)
            .MaximumLength(255).WithMessage("El detalle de pago no puede superar los 255 caracteres.");
    }
}
