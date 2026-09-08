using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IDepartmentService
{
    Task<ResponseGetObject> GetAllDepartments(PaginationQueryFilter paginationQueryFilter, DepartmentQueryFilter departmentQueryFilter);
    Task<ResponsePost> InsertDepartment(DepartmentDto departmentDto);
    Task<ResponsePost> UpdateDepartment(long id, DepartmentDto departmentDto);
    Task<ResponsePost> DeleteDepartment(long id);
}