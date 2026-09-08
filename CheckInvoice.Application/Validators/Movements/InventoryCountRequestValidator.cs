using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class InventoryCountRequestValidator : AbstractValidator<InventoryCountRequestDto>
{
    public InventoryCountRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("WarehouseId is required.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("EndDate must not be earlier than StartDate.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("An inventory count must have at least one detail line.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("ProductId is required.");

            detail.RuleFor(d => d.PhysicalQuantity)
                .GreaterThanOrEqualTo(0).WithMessage("PhysicalQuantity must not be negative.");

            detail.RuleFor(d => d.Notes)
                .MaximumLength(255).WithMessage("Notes must not exceed 255 characters.");
        });
    }
}
