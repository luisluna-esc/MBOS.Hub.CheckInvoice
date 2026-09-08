using CheckInvoice.Application.Dtos.Catalogs;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Catalogs;

public class ShipmentValidator : AbstractValidator<ShipmentDto>
{
    public ShipmentValidator()
    {
        RuleFor(x => x.ShipmentNumber)
            .GreaterThan(0).WithMessage("ShipmentNumber must be greater than zero.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description must not exceed 255 characters.");
    }
}