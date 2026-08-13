using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class IssueRequestValidator : AbstractValidator<IssueRequestDto>
{
    public IssueRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("WarehouseId is required.");

        RuleFor(x => x.Complement)
            .MaximumLength(10).WithMessage("Complement must not exceed 10 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description must not exceed 255 characters.");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("An issue must have at least one detail line.");

        RuleFor(x => x.ClientId)
            .NotNull().WithMessage("ClientId is required when SendToAccountsReceivable is true.")
            .When(x => x.SendToAccountsReceivable);

        RuleFor(x => x.PaymentType)
            .NotEmpty().WithMessage("PaymentType is required when SendToAccountsReceivable is true.")
            .MaximumLength(20).WithMessage("PaymentType must not exceed 20 characters.")
            .When(x => x.SendToAccountsReceivable);

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("ProductId is required.");

            detail.RuleFor(d => d.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.");
        });
    }
}