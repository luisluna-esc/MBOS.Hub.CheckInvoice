using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;

namespace CheckInvoice.Application.Interfaces.Governance;

public interface IInboxService
{
    Task<ResponseGetObject> GetCounts();
    Task<ResponseGetObject> GetItems(InboxQueryDto query);
    Task<ResponsePost> MarkSeen(List<string>? keys);
}
