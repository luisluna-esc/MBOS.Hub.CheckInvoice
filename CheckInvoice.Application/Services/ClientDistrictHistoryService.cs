using System.Net;
using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Interfaces.Parties;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Organization;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Parties;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class ClientDistrictHistoryService : IClientDistrictHistoryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ClientDistrictHistoryDto> _validator;

    public ClientDistrictHistoryService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ClientDistrictHistoryDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllClientDistrictHistories(PaginationQueryFilter paginationQueryFilter, ClientDistrictHistoryQueryFilter clientDistrictHistoryQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<ClientDistrictHistory>().Query();

        if (clientDistrictHistoryQueryFilter.ClientId.HasValue)
        {
            query = query.Where(h => h.ClientId == clientDistrictHistoryQueryFilter.ClientId.Value);
        }

        if (clientDistrictHistoryQueryFilter.DistrictId.HasValue)
        {
            query = query.Where(h => h.DistrictId == clientDistrictHistoryQueryFilter.DistrictId.Value);
        }

        var totalRecords = await query.CountAsync();

        var histories = await query
            .OrderByDescending(h => h.StartDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ClientDistrictHistoryDto>
            {
                Items = histories.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertClientDistrictHistory(ClientDistrictHistoryDto clientDistrictHistoryDto)
    {
        var validationResult = await _validator.ValidateAsync(clientDistrictHistoryDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(clientDistrictHistoryDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var history = new ClientDistrictHistory
        {
            ClientId = clientDistrictHistoryDto.ClientId,
            DistrictId = clientDistrictHistoryDto.DistrictId,
            StartDate = clientDistrictHistoryDto.StartDate,
            EndDate = clientDistrictHistoryDto.EndDate
        };

        await _unitOfWork.Repository<ClientDistrictHistory>().AddAsync(history);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = history.ClientDistrictHistoryId,
            Messages = [new Message { Type = MessageType.Success, Description = "ClientDistrictHistory created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateClientDistrictHistory(long id, ClientDistrictHistoryDto clientDistrictHistoryDto)
    {
        var validationResult = await _validator.ValidateAsync(clientDistrictHistoryDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(clientDistrictHistoryDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<ClientDistrictHistory>();
        var history = await repository.GetByIdAsync(id);

        if (history is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "ClientDistrictHistory not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        history.ClientId = clientDistrictHistoryDto.ClientId;
        history.DistrictId = clientDistrictHistoryDto.DistrictId;
        history.StartDate = clientDistrictHistoryDto.StartDate;
        history.EndDate = clientDistrictHistoryDto.EndDate;

        repository.Update(history);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = history.ClientDistrictHistoryId,
            Messages = [new Message { Type = MessageType.Success, Description = "ClientDistrictHistory updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteClientDistrictHistory(long id)
    {
        var repository = _unitOfWork.Repository<ClientDistrictHistory>();
        var history = await repository.GetByIdAsync(id);

        if (history is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "ClientDistrictHistory not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(history);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = history.ClientDistrictHistoryId,
            Messages = [new Message { Type = MessageType.Success, Description = "ClientDistrictHistory deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task ValidateForeignKeys(ClientDistrictHistoryDto clientDistrictHistoryDto, List<Message> errors)
    {
        if (!await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == clientDistrictHistoryDto.ClientId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ClientId does not reference an existing client." });
        }

        if (!await _unitOfWork.Repository<District>().Query().AnyAsync(d => d.DistrictId == clientDistrictHistoryDto.DistrictId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "DistrictId does not reference an existing district." });
        }
    }

    private static ClientDistrictHistoryDto ToDto(ClientDistrictHistory history) => new()
    {
        ClientDistrictHistoryId = history.ClientDistrictHistoryId,
        ClientId = history.ClientId,
        DistrictId = history.DistrictId,
        StartDate = history.StartDate,
        EndDate = history.EndDate
    };
}