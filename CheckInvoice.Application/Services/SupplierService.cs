using System.Net;
using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Dtos.StoredProcedures;
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

    // EF Core mapea las columnas del resultado de FromSql/SqlQueryRaw por el nombre exacto
    // de la propiedad C# (ej. "SupplierId"), no por el nombre de columna en snake_case que
    // devuelve la función SQL (ej. supplier_id) — de ahí los alias explícitos.
    private const string GetSuppliersSql = """
        SELECT
            supplier_id AS "SupplierId",
            party_id AS "PartyId",
            code AS "Code",
            legal_name AS "LegalName",
            name AS "Name",
            tax_id AS "TaxId",
            country_id AS "CountryId",
            address AS "Address",
            phone AS "Phone",
            mobile_phone AS "MobilePhone",
            email AS "Email",
            notes AS "Notes",
            is_active AS "IsActive",
            linked_client_id AS "LinkedClientId",
            linked_client_name AS "LinkedClientName",
            total_records AS "TotalRecords"
        FROM sp_get_suppliers(
            {0}::bigint, {1}::varchar, {2}::varchar, {3}::bigint, {4}::varchar, {5}::boolean, {6}::int, {7}::int
        )
        """;

    public async Task<ResponseGetObject> GetAllSuppliers(PaginationQueryFilter paginationQueryFilter, SupplierQueryFilter supplierQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Piloto de rendimiento: antes esto hacía 2 consultas separadas (página con join a
        // party + diccionario de clientes vinculados a la misma identidad). Ahora es una sola
        // con LEFT JOIN via sp_get_suppliers (Scrips/storedProcedures.sql).
        var rows = await _unitOfWork.SqlQueryAsync<SupplierGetAllRow>(
            GetSuppliersSql,
            (object?)supplierQueryFilter.SupplierId ?? DBNull.Value,
            (object?)supplierQueryFilter.Code ?? DBNull.Value,
            (object?)supplierQueryFilter.TaxId ?? DBNull.Value,
            (object?)supplierQueryFilter.CountryId ?? DBNull.Value,
            (object?)paginationQueryFilter.SearchCriteria ?? DBNull.Value,
            (object?)supplierQueryFilter.IsActive ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        return new ResponseGetObject
        {
            Data = new PagedResult<SupplierDto>
            {
                Items = rows,
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

        Party? linkedParty = null;
        if (supplierDto.PartyId.HasValue)
        {
            linkedParty = await _unitOfWork.Repository<Party>().GetByIdAsync(supplierDto.PartyId.Value);
            if (linkedParty is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "PartyId does not reference an existing identity." });
            }
            else if (await _unitOfWork.Repository<Supplier>().Query().AnyAsync(s => s.PartyId == linkedParty.PartyId))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "This identity is already registered as a supplier." });
            }
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

        var party = linkedParty;
        if (party is null)
        {
            party = new Party
            {
                Name = supplierDto.Name,
                TaxId = supplierDto.TaxId,
                Email = supplierDto.Email,
                MobilePhone = supplierDto.MobilePhone
            };
            await _unitOfWork.Repository<Party>().AddAsync(party);
            await _unitOfWork.SaveChangesAsync();
        }

        var supplier = new Supplier
        {
            PartyId = party.PartyId,
            LegalName = supplierDto.LegalName,
            CountryId = supplierDto.CountryId,
            Address = supplierDto.Address,
            Phone = supplierDto.Phone,
            Notes = supplierDto.Notes,
            IsActive = supplierDto.IsActive
        };

        var repository = _unitOfWork.Repository<Supplier>();
        await repository.AddAsync(supplier);
        await _unitOfWork.SaveChangesAsync();

        // El código se genera a partir del SupplierId asignado por la base de datos, no lo envía el cliente.
        supplier.Code = supplier.SupplierId.ToString("D5");
        repository.Update(supplier);
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

        // El vínculo de identidad no se cambia desde aquí: se excluye la propia
        // Party para que la validación de NIT/correo únicos no choque contra sí misma.
        supplierDto.PartyId = supplier.PartyId;

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

        var party = await _unitOfWork.Repository<Party>().GetByIdAsync(supplier.PartyId);
        if (party is not null)
        {
            party.Name = supplierDto.Name;
            party.TaxId = supplierDto.TaxId;
            party.Email = supplierDto.Email;
            party.MobilePhone = supplierDto.MobilePhone;
            _unitOfWork.Repository<Party>().Update(party);
        }

        // Code no se toca aquí: se genera una sola vez al crear y es inmutable.
        supplier.LegalName = supplierDto.LegalName;
        supplier.CountryId = supplierDto.CountryId;
        supplier.Address = supplierDto.Address;
        supplier.Phone = supplierDto.Phone;
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

}
