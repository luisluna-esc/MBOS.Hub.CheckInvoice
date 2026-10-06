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
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.")
            .When(x => x.Details.Count == 0);

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.IssueDetailId)
                .GreaterThan(0).WithMessage("Cada monto debe indicar su producto.");
            detail.RuleFor(d => d.Amount)
                .GreaterThan(0).WithMessage("Cada monto por producto debe ser mayor a cero.");
        });

        RuleFor(x => x.Details)
            .Must(details => details.Select(d => d.IssueDetailId).Distinct().Count() == details.Count)
            .WithMessage("Cada producto puede aparecer una sola vez en el depósito.");

        RuleFor(x => x.PaymentMethod)
            .NotEmpty().WithMessage("Selecciona el método de pago.")
            .Must(method => method is "cash" or "transfer").WithMessage("El método de pago debe ser efectivo o transacción.")
            .When(x => !string.IsNullOrEmpty(x.PaymentMethod), ApplyConditionTo.CurrentValidator);

        RuleFor(x => x.Notes)
            .MaximumLength(255).WithMessage("Las notas no pueden superar los 255 caracteres.");
    }
}
