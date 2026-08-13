using CheckInvoice.Application.Dtos.Organization;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Organization;

public class DistrictValidator : AbstractValidator<DistrictDto>
{
    public DistrictValidator()
    {
        RuleFor(x => x.Code)
            .MaximumLength(20).WithMessage("Code must not exceed 20 characters.");

        RuleFor(x => x.DistrictName)
            .NotEmpty().WithMessage("DistrictName is required.")
            .MaximumLength(100).WithMessage("DistrictName must not exceed 100 characters.");
    }
}