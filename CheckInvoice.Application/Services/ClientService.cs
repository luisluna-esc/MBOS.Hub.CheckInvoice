using System.Net;
using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Interfaces.Parties;
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

    public ClientService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ClientDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllClients(PaginationQueryFilter paginationQueryFilter, ClientQueryFilter clientQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Client>().Query();

        if (clientQueryFilter.ClientId.HasValue)
        {
            query = query.Where(c => c.ClientId == clientQueryFilter.ClientId.Value);
        }

        if (clientQueryFilter.AppUserId.HasValue)
        {
            query = query.Where(c => c.AppUserId == clientQueryFilter.AppUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(clientQueryFilter.TaxId))
        {
            query = query.Where(c => c.TaxId == clientQueryFilter.TaxId);
        }

        if (clientQueryFilter.DistrictId.HasValue)
        {
            query = query.Where(c => c.DistrictId == clientQueryFilter.DistrictId.Value);
        }

        if (clientQueryFilter.ChurchId.HasValue)
        {
            query = query.Where(c => c.ChurchId == clientQueryFilter.ChurchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(c => c.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (clientQueryFilter.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == clientQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var clients = await query
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ClientDto>
            {
                Items = clients.Select(ToDto),
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

        if (!string.IsNullOrWhiteSpace(clientDto.TaxId) &&
            await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.TaxId == clientDto.TaxId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This TaxId is already in use." });
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

        var client = new Client
        {
            AppUserId = clientDto.AppUserId,
            DocumentTypeId = clientDto.DocumentTypeId,
            TaxId = clientDto.TaxId,
            Name = clientDto.Name,
            Email = clientDto.Email,
            MobilePhone = clientDto.MobilePhone,
            DistrictId = clientDto.DistrictId,
            ChurchId = clientDto.ChurchId,
            IsActive = clientDto.IsActive,
            Complement = clientDto.Complement
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
        var validationResult = await _validator.ValidateAsync(clientDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(clientDto, errors);

        if (!string.IsNullOrWhiteSpace(clientDto.TaxId) &&
            await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.TaxId == clientDto.TaxId && c.ClientId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This TaxId is already in use." });
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

        client.AppUserId = clientDto.AppUserId;
        client.DocumentTypeId = clientDto.DocumentTypeId;
        client.TaxId = clientDto.TaxId;
        client.Name = clientDto.Name;
        client.Email = clientDto.Email;
        client.MobilePhone = clientDto.MobilePhone;
        client.DistrictId = clientDto.DistrictId;
        client.ChurchId = clientDto.ChurchId;
        client.IsActive = clientDto.IsActive;
        client.Complement = clientDto.Complement;

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
    }

    private static ClientDto ToDto(Client client) => new()
    {
        ClientId = client.ClientId,
        AppUserId = client.AppUserId,
        DocumentTypeId = client.DocumentTypeId,
        TaxId = client.TaxId,
        Name = client.Name,
        Email = client.Email,
        MobilePhone = client.MobilePhone,
        DistrictId = client.DistrictId,
        ChurchId = client.ChurchId,
        IsActive = client.IsActive,
        Complement = client.Complement
    };
}