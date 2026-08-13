using CheckInvoice.Application.Dtos.Catalogs;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Catalogs;

public class PrintTypeValidator : AbstractValidator<PrintTypeDto>
{
    public PrintTypeValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(50).WithMessage("Name must not exceed 50 characters.");
    }
}