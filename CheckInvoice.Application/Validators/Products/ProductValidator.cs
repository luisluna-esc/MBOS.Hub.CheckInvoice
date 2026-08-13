using CheckInvoice.Application.Dtos.Products;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Products;

public class ProductValidator : AbstractValidator<ProductDto>
{
    public ProductValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.UnitOfMeasure)
            .MaximumLength(20).WithMessage("UnitOfMeasure must not exceed 20 characters.");

        RuleFor(x => x.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price must not be negative.")
            .When(x => x.Price.HasValue);

        RuleFor(x => x.MinStock)
            .GreaterThanOrEqualTo(0).WithMessage("MinStock must not be negative.")
            .When(x => x.MinStock.HasValue);

        RuleFor(x => x.MaxStock)
            .GreaterThanOrEqualTo(0).WithMessage("MaxStock must not be negative.")
            .When(x => x.MaxStock.HasValue);
    }
}