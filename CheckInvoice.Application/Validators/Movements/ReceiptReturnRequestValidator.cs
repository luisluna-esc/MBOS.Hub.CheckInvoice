using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class ReceiptReturnRequestValidator : AbstractValidator<ReceiptReturnRequestDto>
{
    public ReceiptReturnRequestValidator()
    {
        RuleFor(x => x.IssueId)
            .GreaterThan(0).WithMessage("Debes indicar la salida de origen.");

        RuleFor(x => x.ReceiptTypeId)
            .GreaterThan(0).WithMessage("Debes seleccionar un tipo de entrada.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("La descripción no puede superar los 255 caracteres.");

        RuleFor(x => x.Lines)
            .NotEmpty().WithMessage("La devolución debe tener al menos una línea.");

        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.ProductId)
                .GreaterThan(0).WithMessage("Debes seleccionar un producto.");

            line.RuleFor(l => l.Quantity)
                .GreaterThan(0).WithMessage("La cantidad debe ser mayor a cero.");
        });
    }
}
