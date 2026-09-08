using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class DiscountValidator : AbstractValidator<DiscountDto>
{
    public static readonly string[] ValidSourceTables = ["product", "account_receivable"];

    public DiscountValidator()
    {
        RuleFor(x => x.SourceTable)
            .NotEmpty().WithMessage("SourceTable is required.")
            .Must(value => ValidSourceTables.Contains(value))
            .WithMessage($"SourceTable must be one of: {string.Join(", ", ValidSourceTables)}.");

        RuleFor(x => x.SourceId)
            .GreaterThan(0).WithMessage("SourceId is required.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("Amount must be greater than zero.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description must not exceed 255 characters.");
    }
}