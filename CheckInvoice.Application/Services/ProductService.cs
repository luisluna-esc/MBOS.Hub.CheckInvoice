using System.Net;
using CheckInvoice.Application.Dtos.Products;
using CheckInvoice.Application.Interfaces.Products;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
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
            // Sin distinguir mayúsculas y también por código: al cargar productos con el teclado se
            // escribe "p" o "900" y deben aparecer "Papel Bond" o el código 90001.
            var term = paginationQueryFilter.SearchCriteria.Trim().ToLower();
            query = query.Where(p => p.Name.ToLower().Contains(term) || (p.Code != null && p.Code.ToLower().Contains(term)));
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
        else if (productQueryFilter.InStock == true)
        {
            var stockQuery = _unitOfWork.Repository<Stock>().Query();
            query = query.Where(p => stockQuery.Any(s => s.ProductId == p.ProductId && s.Quantity > 0));
        }

        var totalRecords = await query.CountAsync();

        var products = await query
            .OrderBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Solo la fecha de la foto (no el contenido) para saber cuáles tienen.
        var productIds = products.Select(p => p.ProductId).ToList();
        var imageVersions = await _unitOfWork.Repository<ProductImage>().Query()
            .Where(i => productIds.Contains(i.ProductId))
            .Select(i => new { i.ProductId, i.UpdatedAt })
            .ToDictionaryAsync(i => i.ProductId, i => i.UpdatedAt.Ticks);

        return new ResponseGetObject
        {
            Data = new PagedResult<ProductDto>
            {
                Items = products.Select(p =>
                {
                    var dto = ToDto(p);
                    if (imageVersions.TryGetValue(p.ProductId, out var version))
                    {
                        dto.HasImage = true;
                        dto.ImageVersion = version;
                    }
                    return dto;
                }),
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
            Messages = [new Message { Type = MessageType.Success, Description = "Producto creado correctamente." }],
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
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró el producto." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (await IsProductInUseAsync(id))
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"No se puede editar \"{product.Name}\" porque ya tiene movimientos registrados (entradas, salidas o transferencias)." }],
                StatusCode = HttpStatusCode.BadRequest
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
            Messages = [new Message { Type = MessageType.Success, Description = "Producto actualizado correctamente." }],
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
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró el producto." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (await IsProductInUseAsync(id))
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"No se puede eliminar \"{product.Name}\" porque ya tiene movimientos registrados (entradas, salidas o transferencias)." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        repository.Remove(product);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = product.ProductId,
            Messages = [new Message { Type = MessageType.Success, Description = "Producto eliminado correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    // "En uso" = tiene al menos un movimiento histórico (Entrada, Salida o Transferencia).
    // Editarlo o eliminarlo en ese punto rompería reportes/kardex que ya lo referencian por ese
    // ProductId, o directamente fallaría por la FK — mejor avisar con un mensaje claro antes.
    private async Task<bool> IsProductInUseAsync(long productId)
    {
        var hasReceiptDetail = await _unitOfWork.Repository<ReceiptDetail>().Query().AnyAsync(d => d.ProductId == productId);
        var hasIssueDetail = await _unitOfWork.Repository<IssueDetail>().Query().AnyAsync(d => d.ProductId == productId);
        var hasTransferDetail = await _unitOfWork.Repository<TransferDetail>().Query().AnyAsync(d => d.ProductId == productId);

        return hasReceiptDetail || hasIssueDetail || hasTransferDetail;
    }

    private async Task ValidateForeignKeys(ProductDto productDto, List<Message> errors)
    {
        if (productDto.DepartmentId.HasValue &&
            !await _unitOfWork.Repository<Department>().Query().AnyAsync(d => d.DepartmentId == productDto.DepartmentId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "El departamento seleccionado no existe." });
        }

        if (productDto.SubDepartmentId.HasValue &&
            !await _unitOfWork.Repository<SubDepartment>().Query().AnyAsync(s => s.SubDepartmentId == productDto.SubDepartmentId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "El subdepartamento seleccionado no existe." });
        }

        if (productDto.MediaTypeId.HasValue &&
            !await _unitOfWork.Repository<MediaType>().Query().AnyAsync(m => m.MediaTypeId == productDto.MediaTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "La unidad de medida seleccionada no existe." });
        }
    }

    public const int MaxImageBytes = 5 * 1024 * 1024;
    private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];

    public async Task<(byte[] Content, string ContentType)?> GetProductImage(long id)
    {
        var image = await _unitOfWork.Repository<ProductImage>().Query().FirstOrDefaultAsync(i => i.ProductId == id);
        return image is null ? null : (image.Content, image.ContentType);
    }

    public async Task<ResponsePost> SetProductImage(long id, byte[] content, string contentType)
    {
        if (!await _unitOfWork.Repository<Product>().Query().AnyAsync(p => p.ProductId == id))
        {
            return ImageError(id, "No se encontró el producto.", HttpStatusCode.NotFound);
        }

        if (!AllowedImageTypes.Contains(contentType))
        {
            return ImageError(id, "La foto debe ser JPG, PNG o WEBP.", HttpStatusCode.BadRequest);
        }

        if (content.Length == 0 || content.Length > MaxImageBytes)
        {
            return ImageError(id, "La foto no puede pesar más de 5 MB.", HttpStatusCode.BadRequest);
        }

        var repository = _unitOfWork.Repository<ProductImage>();
        var image = await repository.Query().FirstOrDefaultAsync(i => i.ProductId == id);
        if (image is null)
        {
            await repository.AddAsync(new ProductImage { ProductId = id, Content = content, ContentType = contentType, UpdatedAt = DateTime.UtcNow });
        }
        else
        {
            image.Content = content;
            image.ContentType = contentType;
            image.UpdatedAt = DateTime.UtcNow;
            repository.Update(image);
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = id,
            Messages = [new Message { Type = MessageType.Success, Description = "Foto guardada correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteProductImage(long id)
    {
        var repository = _unitOfWork.Repository<ProductImage>();
        var image = await repository.Query().FirstOrDefaultAsync(i => i.ProductId == id);
        if (image is not null)
        {
            repository.Remove(image);
            await _unitOfWork.SaveChangesAsync();
        }

        return new ResponsePost
        {
            Id = id,
            Messages = [new Message { Type = MessageType.Success, Description = "Foto quitada correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ResponsePost ImageError(long id, string message, HttpStatusCode statusCode) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = message }],
        StatusCode = statusCode
    };

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