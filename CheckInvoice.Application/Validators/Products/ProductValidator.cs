using CheckInvoice.Application.Dtos.Products;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Products;

public class ProductValidator : AbstractValidator<ProductDto>
{
    public ProductValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(20).WithMessage("El código no puede superar los 20 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(200).WithMessage("El nombre no puede superar los 200 caracteres.");
    }
}
