using CheckInvoice.Application.Dtos.Catalogs;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Catalogs;

public class MissionValidator : AbstractValidator<MissionDto>
{
    public MissionValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(100).WithMessage("Name must not exceed 100 characters.");
    }
}