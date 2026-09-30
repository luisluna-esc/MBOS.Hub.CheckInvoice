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
            .NotEmpty().WithMessage("Debes indicar el tipo de registro.")
            .Must(t => ValidTableNames.Contains(t?.Trim().ToLowerInvariant()))
            .WithMessage("El tipo de registro no es válido.");

        RuleFor(x => x.RecordId)
            .GreaterThan(0).WithMessage("Debes indicar el registro a modificar.");

        RuleFor(x => x.Action)
            .NotEmpty().WithMessage("Debes indicar la acción a realizar.")
            .Must(a => ValidActions.Contains(a?.Trim().ToLowerInvariant()))
            .WithMessage("La acción no es válida.");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("El motivo es obligatorio.")
            .MaximumLength(255).WithMessage("El motivo no puede superar los 255 caracteres.");

        RuleFor(x => x.ProposedData)
            .NotEmpty().WithMessage("Debes indicar los datos propuestos para editar.")
            .When(x => string.Equals(x.Action?.Trim(), "edit", StringComparison.OrdinalIgnoreCase));
    }
}
