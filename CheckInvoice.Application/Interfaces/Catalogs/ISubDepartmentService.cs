using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface ISubDepartmentService
{
    Task<ResponseGetObject> GetAllSubDepartments(PaginationQueryFilter paginationQueryFilter, SubDepartmentQueryFilter subDepartmentQueryFilter);
    Task<ResponsePost> InsertSubDepartment(SubDepartmentDto subDepartmentDto);
    Task<ResponsePost> UpdateSubDepartment(long id, SubDepartmentDto subDepartmentDto);
    Task<ResponsePost> DeleteSubDepartment(long id);
}