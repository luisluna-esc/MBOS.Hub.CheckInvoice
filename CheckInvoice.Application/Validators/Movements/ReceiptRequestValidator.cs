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
            .MaximumLength(20).WithMessage("El NIT o CI no puede superar los 20 caracteres.");

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(50).WithMessage("El número de factura o documento no puede superar los 50 caracteres.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("La descripción no puede superar los 255 caracteres.");

        // Obligatorio solo en compras: lo revisa ReceiptService, que conoce el tipo de entrada.
        RuleFor(x => x.InvoiceTotal)
            .GreaterThan(0).WithMessage("El valor total de la compra debe ser mayor a cero.")
            .When(x => x.InvoiceTotal.HasValue);

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("La entrada debe tener al menos una línea de producto.")
            // Un producto por entrada.
            .Must(details => details is null || details.Select(d => d.ProductId).Distinct().Count() == details.Count)
            .WithMessage("No se puede repetir el mismo producto en más de una línea.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("Debes seleccionar un producto.");

            detail.RuleFor(d => d.Quantity)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");

            detail.RuleFor(d => d.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.");
        });
    }
}
