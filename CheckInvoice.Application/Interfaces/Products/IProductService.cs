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
    Task<(byte[] Content, string ContentType)?> GetProductImage(long id);
    Task<ResponsePost> SetProductImage(long id, byte[] content, string contentType);
    Task<ResponsePost> DeleteProductImage(long id);
}