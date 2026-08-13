using CheckInvoice.Application.Dtos.Parties;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Parties;

public class SupplierValidator : AbstractValidator<SupplierDto>
{
    public SupplierValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");

        RuleFor(x => x.LegalName)
            .MaximumLength(150).WithMessage("LegalName must not exceed 150 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.TaxId)
            .MaximumLength(20).WithMessage("TaxId must not exceed 20 characters.");

        RuleFor(x => x.Address)
            .MaximumLength(255).WithMessage("Address must not exceed 255 characters.");

        RuleFor(x => x.Phone)
            .MaximumLength(20).WithMessage("Phone must not exceed 20 characters.");

        RuleFor(x => x.MobilePhone)
            .MaximumLength(20).WithMessage("MobilePhone must not exceed 20 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");

        RuleFor(x => x.Notes)
            .MaximumLength(255).WithMessage("Notes must not exceed 255 characters.");
    }
}