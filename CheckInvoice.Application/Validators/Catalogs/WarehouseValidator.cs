using CheckInvoice.Application.Dtos.Catalogs;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Catalogs;

public class WarehouseValidator : AbstractValidator<WarehouseDto>
{
    public WarehouseValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(255).WithMessage("Address must not exceed 255 characters.");
    }
}