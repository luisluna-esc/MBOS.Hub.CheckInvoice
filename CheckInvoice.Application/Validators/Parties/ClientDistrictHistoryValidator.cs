using CheckInvoice.Application.Dtos.Parties;
using FluentValidation;

namespace CheckInvoice.Application.Validators.Parties;

public class ClientDistrictHistoryValidator : AbstractValidator<ClientDistrictHistoryDto>
{
    public ClientDistrictHistoryValidator()
    {
        RuleFor(x => x.ClientId)
            .GreaterThan(0).WithMessage("ClientId is required.");

        RuleFor(x => x.DistrictId)
            .GreaterThan(0).WithMessage("DistrictId is required.");

        RuleFor(x => x.EndDate)
            .GreaterThanOrEqualTo(x => x.StartDate).WithMessage("EndDate must not be earlier than StartDate.")
            .When(x => x.EndDate.HasValue);
    }
}