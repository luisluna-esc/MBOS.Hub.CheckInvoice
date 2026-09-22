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
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(20).WithMessage("Name must not exceed 20 characters.")
            .Matches(@"^\d{4}-(0[1-9]|1[0-2])$").WithMessage("Name must use the \"YYYY-MM\" format (e.g. 2026-10).");
    }
}