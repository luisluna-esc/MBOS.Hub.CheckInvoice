using CheckInvoice.Application.Dtos.Parties;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Parties;

public class ClientValidator : AbstractValidator<ClientDto>
{
    public ClientValidator()
    {
        RuleFor(x => x.TaxId)
            .MaximumLength(20).WithMessage("TaxId must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");

        RuleFor(x => x.MobilePhone)
            .MaximumLength(20).WithMessage("MobilePhone must not exceed 20 characters.");

        RuleFor(x => x.Complement)
            .MaximumLength(10).WithMessage("Complement must not exceed 10 characters.");
    }
}