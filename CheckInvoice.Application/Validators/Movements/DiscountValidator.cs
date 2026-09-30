using CheckInvoice.Application.Dtos.Movements;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Movements;

public class DiscountValidator : AbstractValidator<DiscountDto>
{
    public static readonly string[] ValidSourceTables = ["product", "account_receivable"];

    public DiscountValidator()
    {
        RuleFor(x => x.SourceTable)
            .NotEmpty().WithMessage("Debes indicar el origen del descuento.")
            .Must(value => ValidSourceTables.Contains(value))
            .WithMessage("El origen del descuento no es válido.");

        RuleFor(x => x.SourceId)
            .GreaterThan(0).WithMessage("Debes indicar el registro de origen.");

        RuleFor(x => x.Amount)
            .GreaterThan(0).WithMessage("El monto debe ser mayor a cero.");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("La descripción no puede superar los 255 caracteres.");
    }
}