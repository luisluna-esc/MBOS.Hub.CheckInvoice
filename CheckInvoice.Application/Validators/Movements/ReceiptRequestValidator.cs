using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class ReceiptRequestValidator : AbstractValidator<ReceiptRequestDto>
{
    public ReceiptRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("Debes seleccionar un almacén.");

        RuleFor(x => x.TaxId)
            .MaximumLength(20).WithMessage("El NIT no puede superar los 20 caracteres.");

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(50).WithMessage("El número de factura no puede superar los 50 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("La descripción no puede superar los 255 caracteres.");

        RuleFor(x => x.InvoiceTotal)
            .GreaterThanOrEqualTo(0).WithMessage("El total de la factura no puede ser negativo.")
            .When(x => x.InvoiceTotal.HasValue);

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("La entrada debe tener al menos una línea de producto.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("Debes seleccionar un producto.");

            detail.RuleFor(d => d.Quantity)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");

            detail.RuleFor(d => d.UnitCost)
                .GreaterThanOrEqualTo(0).WithMessage("El costo unitario no puede ser negativo.");

            detail.RuleFor(d => d.WorkOrder)
                .MaximumLength(50).WithMessage("La orden de trabajo no puede superar los 50 caracteres.");

            detail.RuleFor(d => d.Detail)
                .MaximumLength(255).WithMessage("El detalle no puede superar los 255 caracteres.");
        });
    }
}
