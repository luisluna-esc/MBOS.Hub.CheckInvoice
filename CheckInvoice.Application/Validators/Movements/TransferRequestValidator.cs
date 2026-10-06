using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class TransferRequestValidator : AbstractValidator<TransferRequestDto>
{
    public TransferRequestValidator()
    {
        RuleFor(x => x.SourceWarehouseId)
            .GreaterThan(0).WithMessage("El almacén de origen no es válido.")
            .When(x => x.SourceWarehouseId.HasValue);

        RuleFor(x => x.DestinationWarehouseId)
            .GreaterThan(0).WithMessage("Debes seleccionar un almacén de destino.");

        RuleFor(x => x)
            .Must(x => x.DestinationWarehouseId != x.SourceWarehouseId!.Value)
            .When(x => x.SourceWarehouseId.HasValue)
            .WithMessage("El almacén de destino debe ser diferente al de origen.")
            .OverridePropertyName("DestinationWarehouseId");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("La transferencia debe tener al menos una línea de producto.")
            // El mismo producto en dos líneas confunde al revisar el documento y al validar stock:
            // se corrige la cantidad en la línea existente en vez de agregar otra.
            .Must(details => details is null || details.Select(d => d.ProductId).Distinct().Count() == details.Count)
            .WithMessage("No se puede repetir el mismo producto en más de una línea.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("Debes seleccionar un producto.");

            detail.RuleFor(d => d.Quantity)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");

            detail.RuleFor(d => d.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("El precio unitario no puede ser negativo.")
                .When(d => d.UnitPrice.HasValue);
        });
    }
}
