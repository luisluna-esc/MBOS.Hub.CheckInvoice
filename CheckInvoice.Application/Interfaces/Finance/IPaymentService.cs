using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Finance;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Finance;

public interface IPaymentService
{
    Task<ResponseGetObject> GetAllPayments(PaginationQueryFilter paginationQueryFilter, PaymentQueryFilter paymentQueryFilter);
    Task<ResponsePost> InsertPayment(PaymentDto paymentDto);
}