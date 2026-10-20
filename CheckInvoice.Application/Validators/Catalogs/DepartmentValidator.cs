using CheckInvoice.Application.Dtos.Catalogs;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Catalogs;

public class DepartmentValidator : AbstractValidator<DepartmentDto>
{
    public DepartmentValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");

        RuleFor(x => x.Code)
            .MaximumLength(20).WithMessage("El código contable no puede pasar de 20 caracteres.");
    }
}