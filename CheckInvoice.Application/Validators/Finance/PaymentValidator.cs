using CheckInvoice.Application.Dtos.Finance;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Finance;

public class PaymentValidator : AbstractValidator<PaymentDto>
{
    public PaymentValidator()
    {
        RuleFor(x => x.AccountReceivableId)
            .GreaterThan(0).WithMessage("Debes indicar la cuenta por cobrar.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");

        RuleFor(x => x.PaymentMethod)
            .MaximumLength(30).WithMessage("El método de pago no puede superar los 30 caracteres.");

        RuleFor(x => x.Notes)
            .MaximumLength(255).WithMessage("Las notas no pueden superar los 255 caracteres.");
    }
}
