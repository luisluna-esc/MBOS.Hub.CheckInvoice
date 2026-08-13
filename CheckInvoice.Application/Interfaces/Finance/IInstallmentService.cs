using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Finance;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Finance;

public interface IInstallmentService
{
    Task<ResponseGetObject> GetAllInstallments(PaginationQueryFilter paginationQueryFilter, InstallmentQueryFilter installmentQueryFilter);
    Task<ResponsePost> InsertInstallment(InstallmentDto installmentDto);
}