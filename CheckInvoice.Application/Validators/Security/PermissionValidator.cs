using CheckInvoice.Application.Dtos.Security;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Security;

public class PermissionValidator : AbstractValidator<PermissionDto>
{
    public PermissionValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Code is required.")
            .MaximumLength(100).WithMessage("Code must not exceed 100 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description must not exceed 255 characters.");

        RuleFor(x => x.Module)
            .NotEmpty().WithMessage("Module is required.")
            .MaximumLength(50).WithMessage("Module must not exceed 50 characters.");
    }
}