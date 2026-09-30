using CheckInvoice.Application.Dtos.Catalogs;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Catalogs;

public class WarehousePeriodValidator : AbstractValidator<WarehousePeriodDto>
{
    public WarehousePeriodValidator()
    {
        // El resto del sistema (períodos vigentes, rango de Fecha de Emisión) parsea este Name
        // como fecha en formato "YYYY-MM" — un valor libre rompería esa lógica en silencio.
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(20).WithMessage("El nombre no puede superar los 20 caracteres.")
            .Matches(@"^\d{4}-(0[1-9]|1[0-2])$").WithMessage("El nombre debe tener el formato \"AAAA-MM\" (ej. 2026-10).");
    }
}