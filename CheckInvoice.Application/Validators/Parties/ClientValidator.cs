using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Validators.Parties;

public class ClientValidator : AbstractValidator<ClientDto>
{
    public ClientValidator(IUnitOfWork unitOfWork)
    {
        RuleFor(x => x.TaxId)
            .MaximumLength(20).WithMessage("El NIT no puede superar los 20 caracteres.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede superar los 150 caracteres.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(150).WithMessage("El correo no puede superar los 150 caracteres.");

        RuleFor(x => x.MobilePhone)
            .MaximumLength(20).WithMessage("El celular no puede superar los 20 caracteres.");

        RuleFor(x => x.Complement)
            .MaximumLength(5).WithMessage("El complemento no puede superar los 5 caracteres.");

        // El NIT y el correo son de la identidad compartida (Party), no del Cliente en
        // sí: se excluye la propia Party (dto.PartyId) para no chocar contra sus propios
        // valores al editar o al vincular con una identidad ya existente.
        RuleFor(x => x.TaxId)
            .MustAsync((dto, taxId, ct) => IsTaxIdUniqueAsync(unitOfWork, dto.PartyId ?? 0, taxId, ct))
            .WithMessage("Este NIT ya está registrado con otra identidad.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId));

        RuleFor(x => x.Email)
            .MustAsync((dto, email, ct) => IsEmailUniqueAsync(unitOfWork, dto.PartyId ?? 0, email, ct))
            .WithMessage("Este correo ya está registrado con otra identidad.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }

    private static Task<bool> IsTaxIdUniqueAsync(IUnitOfWork unitOfWork, long excludePartyId, string? taxId, CancellationToken ct)
    {
        return unitOfWork.Repository<Party>().Query()
            .AllAsync(p => p.PartyId == excludePartyId || p.TaxId != taxId, ct);
    }

    private static Task<bool> IsEmailUniqueAsync(IUnitOfWork unitOfWork, long excludePartyId, string? email, CancellationToken ct)
    {
        var normalized = email!.Trim().ToLower();
        return unitOfWork.Repository<Party>().Query()
            .AllAsync(p => p.PartyId == excludePartyId || p.Email == null || p.Email.ToLower() != normalized, ct);
    }
}
