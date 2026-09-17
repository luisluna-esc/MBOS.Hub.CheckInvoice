using System.Net;
using System.Security.Cryptography;
using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Dtos.StoredProcedures;
using CheckInvoice.Application.Interfaces.Parties;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Organization;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class ClientService : IClientService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ClientDto> _validator;
    private readonly IAppUserService _appUserService;
    private readonly IAppUserRoleService _appUserRoleService;

    public ClientService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<ClientDto> validator,
        IAppUserService appUserService,
        IAppUserRoleService appUserRoleService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _appUserService = appUserService;
        _appUserRoleService = appUserRoleService;
    }

    // EF Core mapea las columnas del resultado de FromSql/SqlQueryRaw por el nombre exacto
    // de la propiedad C# (ej. "ClientId"), no por el nombre de columna en snake_case que
    // devuelve la función SQL (ej. client_id) — de ahí los alias explícitos.
    private const string GetClientsSql = """
        SELECT
            client_id AS "ClientId",
            party_id AS "PartyId",
            app_user_id AS "AppUserId",
            document_type_id AS "DocumentTypeId",
            tax_id AS "TaxId",
            name AS "Name",
            email AS "Email",
            mobile_phone AS "MobilePhone",
            district_id AS "DistrictId",
            church_id AS "ChurchId",
            is_active AS "IsActive",
            complement AS "Complement",
            special_case_id AS "SpecialCaseId",
            is_pastor AS "IsPastor",
            linked_supplier_id AS "LinkedSupplierId",
            linked_supplier_name AS "LinkedSupplierName",
            total_records AS "TotalRecords"
        FROM sp_get_clients(
            {0}::bigint, {1}::bigint, {2}::varchar, {3}::bigint, {4}::bigint,
            {5}::varchar, {6}::boolean, {7}::boolean, {8}::boolean, {9}::int, {10}::int
        )
        """;

    public async Task<ResponseGetObject> GetAllClients(PaginationQueryFilter paginationQueryFilter, ClientQueryFilter clientQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Piloto de rendimiento: antes esto hacía 2 consultas separadas (página con join a
        // party + diccionario de proveedores vinculados a la misma identidad). Ahora es una
        // sola con LEFT JOIN via sp_get_clients (Scrips/storedProcedures.sql).
        var rows = await _unitOfWork.SqlQueryAsync<ClientGetAllRow>(
            GetClientsSql,
            (object?)clientQueryFilter.ClientId ?? DBNull.Value,
            (object?)clientQueryFilter.AppUserId ?? DBNull.Value,
            (object?)clientQueryFilter.TaxId ?? DBNull.Value,
            (object?)clientQueryFilter.DistrictId ?? DBNull.Value,
            (object?)clientQueryFilter.ChurchId ?? DBNull.Value,
            (object?)paginationQueryFilter.SearchCriteria ?? DBNull.Value,
            (object?)clientQueryFilter.IsActive ?? DBNull.Value,
            (object?)clientQueryFilter.IsPastor ?? DBNull.Value,
            (object?)clientQueryFilter.PendingPortalAccess ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        return new ResponseGetObject
        {
            Data = new PagedResult<ClientDto>
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

    public async Task<ResponsePost> InsertClient(ClientDto clientDto)
    {
        var validationResult = await _validator.ValidateAsync(clientDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(clientDto, errors);

        Party? linkedParty = null;
        if (clientDto.PartyId.HasValue)
        {
            linkedParty = await _unitOfWork.Repository<Party>().GetByIdAsync(clientDto.PartyId.Value);
            if (linkedParty is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "PartyId does not reference an existing identity." });
            }
            else if (await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.PartyId == linkedParty.PartyId))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "This identity is already registered as a client." });
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
                Name = clientDto.Name,
                TaxId = clientDto.TaxId,
                Email = clientDto.Email,
                MobilePhone = clientDto.MobilePhone
            };
            await _unitOfWork.Repository<Party>().AddAsync(party);
            await _unitOfWork.SaveChangesAsync();
        }

        var client = new Client
        {
            PartyId = party.PartyId,
            AppUserId = clientDto.AppUserId,
            DocumentTypeId = clientDto.DocumentTypeId,
            DistrictId = clientDto.DistrictId,
            ChurchId = clientDto.ChurchId,
            IsActive = clientDto.IsActive,
            Complement = clientDto.Complement,
            SpecialCaseId = clientDto.SpecialCaseId,
            IsPastor = clientDto.IsPastor
        };

        await _unitOfWork.Repository<Client>().AddAsync(client);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = client.ClientId,
            Messages = [new Message { Type = MessageType.Success, Description = "Client created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateClient(long id, ClientDto clientDto)
    {
        var repository = _unitOfWork.Repository<Client>();
        var client = await repository.GetByIdAsync(id);

        if (client is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Client not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        // El vínculo de identidad no se cambia desde aquí: se excluye la propia
        // Party para que la validación de NIT/correo únicos no choque contra sí misma.
        clientDto.PartyId = client.PartyId;

        var validationResult = await _validator.ValidateAsync(clientDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(clientDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var party = await _unitOfWork.Repository<Party>().GetByIdAsync(client.PartyId);
        if (party is not null)
        {
            party.Name = clientDto.Name;
            party.TaxId = clientDto.TaxId;
            party.Email = clientDto.Email;
            party.MobilePhone = clientDto.MobilePhone;
            _unitOfWork.Repository<Party>().Update(party);
        }

        client.AppUserId = clientDto.AppUserId;
        client.DocumentTypeId = clientDto.DocumentTypeId;
        client.DistrictId = clientDto.DistrictId;
        client.ChurchId = clientDto.ChurchId;
        client.IsActive = clientDto.IsActive;
        client.Complement = clientDto.Complement;
        client.SpecialCaseId = clientDto.SpecialCaseId;
        client.IsPastor = clientDto.IsPastor;

        repository.Update(client);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = client.ClientId,
            Messages = [new Message { Type = MessageType.Success, Description = "Client updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteClient(long id)
    {
        var repository = _unitOfWork.Repository<Client>();
        var client = await repository.GetByIdAsync(id);

        if (client is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Client not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(client);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = client.ClientId,
            Messages = [new Message { Type = MessageType.Success, Description = "Client deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task ValidateForeignKeys(ClientDto clientDto, List<Message> errors)
    {
        if (clientDto.AppUserId.HasValue &&
            !await _unitOfWork.Repository<AppUser>().Query().AnyAsync(u => u.AppUserId == clientDto.AppUserId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "AppUserId does not reference an existing user." });
        }

        if (clientDto.DocumentTypeId.HasValue &&
            !await _unitOfWork.Repository<DocumentType>().Query().AnyAsync(d => d.DocumentTypeId == clientDto.DocumentTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "DocumentTypeId does not reference an existing document type." });
        }

        if (clientDto.DistrictId.HasValue &&
            !await _unitOfWork.Repository<District>().Query().AnyAsync(d => d.DistrictId == clientDto.DistrictId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "DistrictId does not reference an existing district." });
        }

        if (clientDto.ChurchId.HasValue &&
            !await _unitOfWork.Repository<Church>().Query().AnyAsync(c => c.ChurchId == clientDto.ChurchId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ChurchId does not reference an existing church." });
        }

        if (clientDto.SpecialCaseId.HasValue &&
            !await _unitOfWork.Repository<SpecialCase>().Query().AnyAsync(s => s.SpecialCaseId == clientDto.SpecialCaseId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "SpecialCaseId does not reference an existing special case." });
        }
    }

    public async Task<ResponseGetObject> GrantPortalAccess(long id)
    {
        var clientRepository = _unitOfWork.Repository<Client>();
        var client = await clientRepository.GetByIdAsync(id);

        if (client is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Client not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (!client.IsPastor)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "This client is not marked as Pastor." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        if (client.AppUserId.HasValue)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "This client already has portal access." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var party = await _unitOfWork.Repository<Party>().GetByIdAsync(client.PartyId);

        if (party is null || string.IsNullOrWhiteSpace(party.Email))
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "The client needs a registered email before granting portal access." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var pastorRole = await _unitOfWork.Repository<Role>().Query().FirstOrDefaultAsync(r => r.Name == "Pastor");
        if (pastorRole is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "The 'Pastor' role does not exist. Contact an administrator." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var existingUser = await _unitOfWork.Repository<AppUser>().Query()
            .FirstOrDefaultAsync(u => u.Email == party.Email);

        long appUserId;
        string? temporaryPassword = null;

        if (existingUser is not null)
        {
            var alreadyLinked = await clientRepository.Query()
                .AnyAsync(c => c.AppUserId == existingUser.AppUserId && c.ClientId != id);
            if (alreadyLinked)
            {
                return new ResponseGetObject
                {
                    Data = new(),
                    Messages = [new Message { Type = MessageType.Error, Description = "A user account with this email is already linked to another client." }],
                    StatusCode = HttpStatusCode.BadRequest
                };
            }

            appUserId = existingUser.AppUserId;
        }
        else
        {
            temporaryPassword = GenerateTemporaryPassword();
            var nameParts = party.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var insertResult = await _appUserService.InsertAppUser(new AppUserDto
            {
                FirstName = nameParts.Length > 0 ? nameParts[0] : party.Name,
                LastName = nameParts.Length > 1 ? string.Join(' ', nameParts.Skip(1)) : nameParts.Length > 0 ? nameParts[0] : party.Name,
                Email = party.Email,
                Username = party.Email,
                Password = temporaryPassword,
                IsActive = true
            });

            if (insertResult.StatusCode != HttpStatusCode.Created)
            {
                return new ResponseGetObject
                {
                    Data = new(),
                    Messages = insertResult.Messages,
                    StatusCode = insertResult.StatusCode
                };
            }

            appUserId = insertResult.Id;
        }

        var alreadyHasRole = await _unitOfWork.Repository<AppUserRole>().Query()
            .AnyAsync(ur => ur.AppUserId == appUserId && ur.RoleId == pastorRole.RoleId);

        if (!alreadyHasRole)
        {
            await _appUserRoleService.AssignRole(appUserId, pastorRole.RoleId);
        }

        client.AppUserId = appUserId;
        clientRepository.Update(client);
        await _unitOfWork.SaveChangesAsync();

        return new ResponseGetObject
        {
            Data = new { AppUserId = appUserId, TemporaryPassword = temporaryPassword },
            Messages = [new Message { Type = MessageType.Success, Description = "Portal access granted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";
        var bytes = RandomNumberGenerator.GetBytes(12);
        return new string(bytes.Select(b => chars[b % chars.Length]).ToArray());
    }

}
