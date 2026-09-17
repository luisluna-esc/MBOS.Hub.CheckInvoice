using CheckInvoice.Application.Dtos.Governance;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Governance;

public class ChangeRequestCreateValidator : AbstractValidator<ChangeRequestCreateDto>
{
    private static readonly string[] ValidTableNames = ["receipt", "issue", "transfer"];
    private static readonly string[] ValidActions = ["edit", "delete"];

    public ChangeRequestCreateValidator()
    {
        RuleFor(x => x.TableName)
            .NotEmpty().WithMessage("TableName is required.")
            .Must(t => ValidTableNames.Contains(t?.Trim().ToLowerInvariant()))
            .WithMessage($"TableName must be one of: {string.Join(", ", ValidTableNames)}.");

        RuleFor(x => x.RecordId)
            .GreaterThan(0).WithMessage("RecordId is required.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Action is required.")
            .Must(a => ValidActions.Contains(a?.Trim().ToLowerInvariant()))
            .WithMessage($"Action must be one of: {string.Join(", ", ValidActions)}.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Reason is required.")
            .MaximumLength(255).WithMessage("Reason must not exceed 255 characters.");

        RuleFor(x => x.ProposedData)
            .NotEmpty().WithMessage("ProposedData is required when Action is 'edit'.")
            .When(x => string.Equals(x.Action?.Trim(), "edit", StringComparison.OrdinalIgnoreCase));
    }
}
