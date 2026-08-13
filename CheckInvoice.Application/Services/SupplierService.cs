using System.Net;
using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Interfaces.Parties;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class SupplierService : ISupplierService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<SupplierDto> _validator;

    public SupplierService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<SupplierDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllSuppliers(PaginationQueryFilter paginationQueryFilter, SupplierQueryFilter supplierQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Supplier>().Query();

        if (supplierQueryFilter.SupplierId.HasValue)
        {
            query = query.Where(s => s.SupplierId == supplierQueryFilter.SupplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(supplierQueryFilter.Code))
        {
            query = query.Where(s => s.Code == supplierQueryFilter.Code);
        }

        if (!string.IsNullOrWhiteSpace(supplierQueryFilter.TaxId))
        {
            query = query.Where(s => s.TaxId == supplierQueryFilter.TaxId);
        }

        if (supplierQueryFilter.CountryId.HasValue)
        {
            query = query.Where(s => s.CountryId == supplierQueryFilter.CountryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(s => s.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (supplierQueryFilter.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == supplierQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var suppliers = await query
            .OrderBy(s => s.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<SupplierDto>
            {
                Items = suppliers.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertSupplier(SupplierDto supplierDto)
    {
        var validationResult = await _validator.ValidateAsync(supplierDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (supplierDto.CountryId.HasValue &&
            !await _unitOfWork.Repository<Country>().Query().AnyAsync(c => c.CountryId == supplierDto.CountryId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "CountryId does not reference an existing country." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var supplier = new Supplier
        {
            Code = supplierDto.Code,
            LegalName = supplierDto.LegalName,
            Name = supplierDto.Name,
            TaxId = supplierDto.TaxId,
            CountryId = supplierDto.CountryId,
            Address = supplierDto.Address,
            Phone = supplierDto.Phone,
            MobilePhone = supplierDto.MobilePhone,
            Email = supplierDto.Email,
            Notes = supplierDto.Notes,
            IsActive = supplierDto.IsActive
        };

        await _unitOfWork.Repository<Supplier>().AddAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = supplier.SupplierId,
            Messages = [new Message { Type = MessageType.Success, Description = "Supplier created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateSupplier(long id, SupplierDto supplierDto)
    {
        var validationResult = await _validator.ValidateAsync(supplierDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (supplierDto.CountryId.HasValue &&
            !await _unitOfWork.Repository<Country>().Query().AnyAsync(c => c.CountryId == supplierDto.CountryId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "CountryId does not reference an existing country." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Supplier>();
        var supplier = await repository.GetByIdAsync(id);

        if (supplier is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Supplier not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        supplier.Code = supplierDto.Code;
        supplier.LegalName = supplierDto.LegalName;
        supplier.Name = supplierDto.Name;
        supplier.TaxId = supplierDto.TaxId;
        supplier.CountryId = supplierDto.CountryId;
        supplier.Address = supplierDto.Address;
        supplier.Phone = supplierDto.Phone;
        supplier.MobilePhone = supplierDto.MobilePhone;
        supplier.Email = supplierDto.Email;
        supplier.Notes = supplierDto.Notes;
        supplier.IsActive = supplierDto.IsActive;

        repository.Update(supplier);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = supplier.SupplierId,
            Messages = [new Message { Type = MessageType.Success, Description = "Supplier updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteSupplier(long id)
    {
        var repository = _unitOfWork.Repository<Supplier>();
        var supplier = await repository.GetByIdAsync(id);

        if (supplier is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Supplier not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(supplier);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = supplier.SupplierId,
            Messages = [new Message { Type = MessageType.Success, Description = "Supplier deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static SupplierDto ToDto(Supplier supplier) => new()
    {
        SupplierId = supplier.SupplierId,
        Code = supplier.Code,
        LegalName = supplier.LegalName,
        Name = supplier.Name,
        TaxId = supplier.TaxId,
        CountryId = supplier.CountryId,
        Address = supplier.Address,
        Phone = supplier.Phone,
        MobilePhone = supplier.MobilePhone,
        Email = supplier.Email,
        Notes = supplier.Notes,
        IsActive = supplier.IsActive
    };
}