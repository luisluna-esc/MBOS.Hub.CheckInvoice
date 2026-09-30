using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class InventoryCountRequestValidator : AbstractValidator<InventoryCountRequestDto>
{
    public InventoryCountRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("Debes seleccionar un almacén.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("La fecha final no puede ser anterior a la fecha inicial.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("El ajuste de inventario debe tener al menos una línea de producto.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("Debes seleccionar un producto.");

            detail.RuleFor(d => d.PhysicalQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("La cantidad física no puede ser negativa.");

            detail.RuleFor(d => d.Notes)
                .MaximumLength(255).WithMessage("Las notas no pueden superar los 255 caracteres.");
        });
    }
}
