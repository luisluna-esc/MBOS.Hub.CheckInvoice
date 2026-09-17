using System.Net;
using CheckInvoice.Application.Dtos.Products;
using CheckInvoice.Application.Interfaces.Products;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Products;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class ProductService : IProductService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ProductDto> _validator;

    public ProductService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ProductDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllProducts(PaginationQueryFilter paginationQueryFilter, ProductQueryFilter productQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Product>().Query();

        if (productQueryFilter.ProductId.HasValue)
        {
            query = query.Where(p => p.ProductId == productQueryFilter.ProductId.Value);
        }

        if (!string.IsNullOrWhiteSpace(productQueryFilter.Code))
        {
            query = query.Where(p => p.Code != null && p.Code.Contains(productQueryFilter.Code));
        }

        if (productQueryFilter.DepartmentId.HasValue)
        {
            query = query.Where(p => p.DepartmentId == productQueryFilter.DepartmentId.Value);
        }

        if (productQueryFilter.SubDepartmentId.HasValue)
        {
            query = query.Where(p => p.SubDepartmentId == productQueryFilter.SubDepartmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(p => p.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (productQueryFilter.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == productQueryFilter.IsActive.Value);
        }

        if (productQueryFilter.WarehouseId.HasValue)
        {
            var warehouseId = productQueryFilter.WarehouseId.Value;
            var stockQuery = _unitOfWork.Repository<Stock>().Query();
            query = query.Where(p => stockQuery.Any(s =>
                s.ProductId == p.ProductId && s.WarehouseId == warehouseId && s.Quantity > 0));
        }

        var totalRecords = await query.CountAsync();

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ProductDto>
            {
                Items = products.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertProduct(ProductDto productDto)
    {
        var validationResult = await _validator.ValidateAsync(productDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(productDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var product = new Product
        {
            Name = productDto.Name,
            DepartmentId = productDto.DepartmentId,
            SubDepartmentId = productDto.SubDepartmentId,
            MediaTypeId = productDto.MediaTypeId,
            IsActive = productDto.IsActive
        };

        var repository = _unitOfWork.Repository<Product>();
        await repository.AddAsync(product);
        await _unitOfWork.SaveChangesAsync();

        // El código se genera a partir del ProductId asignado por la base de datos, no lo envía el cliente.
        product.Code = product.ProductId.ToString("D5");
        repository.Update(product);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = product.ProductId,
            Messages = [new Message { Type = MessageType.Success, Description = "Product created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateProduct(long id, ProductDto productDto)
    {
        var validationResult = await _validator.ValidateAsync(productDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(productDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Product>();
        var product = await repository.GetByIdAsync(id);

        if (product is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Product not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        // Code no se toca aquí: se genera una sola vez al crear y es inmutable.
        product.Name = productDto.Name;
        product.DepartmentId = productDto.DepartmentId;
        product.SubDepartmentId = productDto.SubDepartmentId;
        product.MediaTypeId = productDto.MediaTypeId;
        product.IsActive = productDto.IsActive;

        repository.Update(product);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = product.ProductId,
            Messages = [new Message { Type = MessageType.Success, Description = "Product updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteProduct(long id)
    {
        var repository = _unitOfWork.Repository<Product>();
        var product = await repository.GetByIdAsync(id);

        if (product is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Product not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(product);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = product.ProductId,
            Messages = [new Message { Type = MessageType.Success, Description = "Product deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task ValidateForeignKeys(ProductDto productDto, List<Message> errors)
    {
        if (productDto.DepartmentId.HasValue &&
            !await _unitOfWork.Repository<Department>().Query().AnyAsync(d => d.DepartmentId == productDto.DepartmentId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "DepartmentId does not reference an existing department." });
        }

        if (productDto.SubDepartmentId.HasValue &&
            !await _unitOfWork.Repository<SubDepartment>().Query().AnyAsync(s => s.SubDepartmentId == productDto.SubDepartmentId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "SubDepartmentId does not reference an existing sub-department." });
        }

        if (productDto.MediaTypeId.HasValue &&
            !await _unitOfWork.Repository<MediaType>().Query().AnyAsync(m => m.MediaTypeId == productDto.MediaTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "MediaTypeId does not reference an existing media type." });
        }
    }

    private static ProductDto ToDto(Product product) => new()
    {
        ProductId = product.ProductId,
        Code = product.Code,
        Name = product.Name,
        DepartmentId = product.DepartmentId,
        SubDepartmentId = product.SubDepartmentId,
        MediaTypeId = product.MediaTypeId,
        IsActive = product.IsActive
    };
}