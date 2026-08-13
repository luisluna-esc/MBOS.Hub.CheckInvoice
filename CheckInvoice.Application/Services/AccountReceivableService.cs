using System.Net;
using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Interfaces.Finance;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Finance;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class AccountReceivableService : IAccountReceivableService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<AccountReceivableDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public AccountReceivableService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<AccountReceivableDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllAccountReceivables(PaginationQueryFilter paginationQueryFilter, AccountReceivableQueryFilter accountReceivableQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<AccountReceivable>().Query();

        if (accountReceivableQueryFilter.AccountReceivableId.HasValue)
        {
            query = query.Where(a => a.AccountReceivableId == accountReceivableQueryFilter.AccountReceivableId.Value);
        }

        if (accountReceivableQueryFilter.ClientId.HasValue)
        {
            query = query.Where(a => a.ClientId == accountReceivableQueryFilter.ClientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(accountReceivableQueryFilter.PaymentType))
        {
            query = query.Where(a => a.PaymentType == accountReceivableQueryFilter.PaymentType);
        }

        if (!string.IsNullOrWhiteSpace(accountReceivableQueryFilter.Status))
        {
            query = query.Where(a => a.Status == accountReceivableQueryFilter.Status);
        }

        var totalRecords = await query.CountAsync();

        var accountReceivables = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<AccountReceivableDto>
            {
                Items = accountReceivables.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertAccountReceivable(AccountReceivableDto accountReceivableDto)
    {
        var validationResult = await _validator.ValidateAsync(accountReceivableDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (accountReceivableDto.IssueId.HasValue &&
            !await _unitOfWork.Repository<Issue>().Query().AnyAsync(i => i.IssueId == accountReceivableDto.IssueId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "IssueId does not reference an existing issue." });
        }

        if (accountReceivableDto.ClientId.HasValue &&
            !await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == accountReceivableDto.ClientId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ClientId does not reference an existing client." });
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

        var accountReceivable = new AccountReceivable
        {
            IssueId = accountReceivableDto.IssueId,
            ClientId = accountReceivableDto.ClientId,
            TotalAmount = accountReceivableDto.TotalAmount,
            OutstandingBalance = accountReceivableDto.TotalAmount,
            PaymentType = accountReceivableDto.PaymentType,
            DueDate = accountReceivableDto.DueDate,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            CreatedById = _currentUserService.AppUserId
        };

        await _unitOfWork.Repository<AccountReceivable>().AddAsync(accountReceivable);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = accountReceivable.AccountReceivableId,
            Messages = [new Message { Type = MessageType.Success, Description = "AccountReceivable created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    private static AccountReceivableDto ToDto(AccountReceivable accountReceivable) => new()
    {
        AccountReceivableId = accountReceivable.AccountReceivableId,
        IssueId = accountReceivable.IssueId,
        ClientId = accountReceivable.ClientId,
        TotalAmount = accountReceivable.TotalAmount,
        OutstandingBalance = accountReceivable.OutstandingBalance,
        PaymentType = accountReceivable.PaymentType,
        DueDate = accountReceivable.DueDate,
        Status = accountReceivable.Status,
        CreatedAt = accountReceivable.CreatedAt,
        CreatedById = accountReceivable.CreatedById
    };
}