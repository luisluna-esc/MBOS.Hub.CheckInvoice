using CheckInvoice.Application.Dtos.Catalogs;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Catalogs;

public class WarehouseValidator : AbstractValidator<WarehouseDto>
{
    public WarehouseValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar los 100 caracteres.");

        RuleFor(x => x.Address)
            .MaximumLength(255).WithMessage("La dirección no puede superar los 255 caracteres.");
    }
}