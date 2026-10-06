using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class IssueRequestValidator : AbstractValidator<IssueRequestDto>
{
    public IssueRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("Debes seleccionar un almacén.");

        RuleFor(x => x.Complement)
            .MaximumLength(10).WithMessage("El complemento no puede superar los 10 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("La descripción no puede superar los 255 caracteres.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("La salida debe tener al menos una línea de producto.")
            // El mismo producto en dos líneas confunde al revisar el documento y al validar stock:
            // se corrige la cantidad en la línea existente en vez de agregar otra.
            .Must(details => details is null || details.Select(d => d.ProductId).Distinct().Count() == details.Count)
            .WithMessage("No se puede repetir el mismo producto en más de una línea.");

        RuleFor(x => x.ClientId)
            .NotNull().WithMessage("Debes seleccionar un cliente para enviar a Cuentas por Cobrar.")
            .When(x => x.SendToAccountsReceivable);

        RuleFor(x => x.PaymentType)
            .NotEmpty().WithMessage("Debes indicar el tipo de pago para enviar a Cuentas por Cobrar.")
            .MaximumLength(20).WithMessage("El tipo de pago no puede superar los 20 caracteres.")
            .When(x => x.SendToAccountsReceivable);

        RuleFor(x => x.PaymentDetail)
            .MaximumLength(255).WithMessage("El detalle de pago no puede superar los 255 caracteres.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("Debes seleccionar un producto.");

            detail.RuleFor(d => d.Quantity)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
        });
    }
}
