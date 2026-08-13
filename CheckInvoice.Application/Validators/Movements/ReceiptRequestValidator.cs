using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class ReceiptRequestValidator : AbstractValidator<ReceiptRequestDto>
{
    public ReceiptRequestValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0).WithMessage("WarehouseId is required.");

        RuleFor(x => x.TaxId)
            .MaximumLength(20).WithMessage("TaxId must not exceed 20 characters.");

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(50).WithMessage("InvoiceNumber must not exceed 50 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Description must not exceed 255 characters.");

        RuleFor(x => x.InvoiceTotal)
            .GreaterThanOrEqualTo(0).WithMessage("InvoiceTotal must not be negative.")
            .When(x => x.InvoiceTotal.HasValue);

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("A receipt must have at least one detail line.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("ProductId is required.");

            detail.RuleFor(d => d.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

            detail.RuleFor(d => d.UnitCost)
                .GreaterThanOrEqualTo(0).WithMessage("UnitCost must not be negative.");

            detail.RuleFor(d => d.WorkOrder)
                .MaximumLength(50).WithMessage("WorkOrder must not exceed 50 characters.");

            detail.RuleFor(d => d.Detail)
                .MaximumLength(255).WithMessage("Detail must not exceed 255 characters.");
        });
    }
}