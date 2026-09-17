using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class ReceiptReturnRequestValidator : AbstractValidator<ReceiptReturnRequestDto>
{
    public ReceiptReturnRequestValidator()
    {
        RuleFor(x => x.IssueId)
            .GreaterThan(0).WithMessage("IssueId is required.");

        RuleFor(x => x.ReceiptTypeId)
            .GreaterThan(0).WithMessage("ReceiptTypeId is required.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description must not exceed 255 characters.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("A return must have at least one line.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .GreaterThan(0).WithMessage("ProductId is required.");

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        });
    }
}
