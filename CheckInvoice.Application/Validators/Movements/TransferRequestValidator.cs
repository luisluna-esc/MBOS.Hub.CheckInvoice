using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class TransferRequestValidator : AbstractValidator<TransferRequestDto>
{
    public TransferRequestValidator()
    {
        RuleFor(x => x.SourceWarehouseId)
            .GreaterThan(0).WithMessage("SourceWarehouseId must reference a valid warehouse.")
            .When(x => x.SourceWarehouseId.HasValue);

        RuleFor(x => x.DestinationWarehouseId)
            .GreaterThan(0).WithMessage("DestinationWarehouseId is required.");

        RuleFor(x => x)
            .Must(x => x.DestinationWarehouseId != x.SourceWarehouseId!.Value)
            .When(x => x.SourceWarehouseId.HasValue)
            .WithMessage("DestinationWarehouseId must be different from SourceWarehouseId.")
            .OverridePropertyName("DestinationWarehouseId");

        RuleFor(x => x.Details)
            .NotEmpty().WithMessage("A transfer must have at least one detail line.");

        RuleForEach(x => x.Details).ChildRules(detail =>
        {
            detail.RuleFor(d => d.ProductId)
                .GreaterThan(0).WithMessage("ProductId is required.");

            detail.RuleFor(d => d.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.");

            detail.RuleFor(d => d.UnitPrice)
                .GreaterThanOrEqualTo(0).WithMessage("UnitPrice must not be negative.")
                .When(d => d.UnitPrice.HasValue);
        });
    }
}