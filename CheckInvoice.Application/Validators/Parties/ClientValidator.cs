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
            .MaximumLength(20).WithMessage("TaxId must not exceed 20 characters.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(150).WithMessage("Name must not exceed 150 characters.");

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .MaximumLength(150).WithMessage("Email must not exceed 150 characters.");

        RuleFor(x => x.MobilePhone)
            .MaximumLength(20).WithMessage("MobilePhone must not exceed 20 characters.");

        RuleFor(x => x.Complement)
            .MaximumLength(5).WithMessage("Complement must not exceed 5 characters.");

        // El NIT y el correo son de la identidad compartida (Party), no del Cliente en
        // sí: se excluye la propia Party (dto.PartyId) para no chocar contra sus propios
        // valores al editar o al vincular con una identidad ya existente.
        RuleFor(x => x.TaxId)
            .MustAsync((dto, taxId, ct) => IsTaxIdUniqueAsync(unitOfWork, dto.PartyId ?? 0, taxId, ct))
            .WithMessage("This Tax ID is already registered to another party.")
            .When(x => !string.IsNullOrWhiteSpace(x.TaxId));

        RuleFor(x => x.Email)
            .MustAsync((dto, email, ct) => IsEmailUniqueAsync(unitOfWork, dto.PartyId ?? 0, email, ct))
            .WithMessage("This email is already registered to another party.")
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
