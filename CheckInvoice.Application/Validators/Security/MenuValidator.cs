using CheckInvoice.Application.Dtos.Security;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Security;

public class MenuValidator : AbstractValidator<MenuDto>
{
    public MenuValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.TranslationKey)
            .MaximumLength(100).WithMessage("TranslationKey must not exceed 100 characters.");

        RuleFor(x => x.Route)
            .MaximumLength(150).WithMessage("Route must not exceed 150 characters.");

        RuleFor(x => x.Icon)
            .MaximumLength(50).WithMessage("Icon must not exceed 50 characters.");

        RuleFor(x => x.DisplayOrder)
            .GreaterThanOrEqualTo(0).WithMessage("DisplayOrder must not be negative.");
    }
}