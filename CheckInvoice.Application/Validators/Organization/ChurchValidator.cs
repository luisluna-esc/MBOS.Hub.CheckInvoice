using CheckInvoice.Application.Dtos.Organization;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Organization;

public class ChurchValidator : AbstractValidator<ChurchDto>
{
    public ChurchValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");

        RuleFor(x => x.ChurchName)
            .NotEmpty().WithMessage("ChurchName is required.")
            .MaximumLength(150).WithMessage("ChurchName must not exceed 150 characters.");
    }
}