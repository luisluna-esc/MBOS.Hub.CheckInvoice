using CheckInvoice.Application.Dtos.Products;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Products;

namespace CheckInvoice.Application.Interfaces.Products;

public interface IProductService
{
    Task<ResponseGetObject> GetAllProducts(PaginationQueryFilter paginationQueryFilter, ProductQueryFilter productQueryFilter);
    Task<ResponsePost> InsertProduct(ProductDto productDto);
    Task<ResponsePost> UpdateProduct(long id, ProductDto productDto);
    Task<ResponsePost> DeleteProduct(long id);
}