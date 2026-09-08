using System.Net;
using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Dtos.Portal;
using CheckInvoice.Application.Interfaces.Portal;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Portal;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class PortalService : IPortalService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly ICurrentUserService _currentUserService;

    public PortalService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetMyAccountReceivables(PaginationQueryFilter paginationQueryFilter)
    {
        var clientId = await ResolveMyClientId();
        if (clientId is null)
        {
            return NoLinkedClientResponse();
        }

        var (pageSize, pageNumber) = ResolvePaging(paginationQueryFilter);

        var query = _unitOfWork.Repository<AccountReceivable>().Query()
            .Where(a => a.ClientId == clientId.Value)
            .OrderByDescending(a => a.CreatedAt);

        var totalRecords = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        var issueIds = items.Where(a => a.IssueId.HasValue).Select(a => a.IssueId!.Value).Distinct().ToList();
        var issueDatesById = await _unitOfWork.Repository<Issue>().Query()
            .Where(i => issueIds.Contains(i.IssueId))
            .ToDictionaryAsync(i => i.IssueId, i => i.IssueDate);

        return new ResponseGetObject
        {
            Data = new PagedResult<AccountReceivableDto>
            {
                Items = items.Select(a => new AccountReceivableDto
                {
                    AccountReceivableId = a.AccountReceivableId,
                    IssueId = a.IssueId,
                    IssueDate = a.IssueId.HasValue && issueDatesById.TryGetValue(a.IssueId.Value, out var issueDate) ? issueDate : null,
                    ClientId = a.ClientId,
                    TotalAmount = a.TotalAmount,
                    OutstandingBalance = a.OutstandingBalance,
                    PaymentType = a.PaymentType,
                    PaymentDetail = a.PaymentDetail,
                    DueDate = a.DueDate,
                    Status = a.Status,
                    CreatedAt = a.CreatedAt,
                    CreatedById = a.CreatedById
                }),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetMyInstallments(PaginationQueryFilter paginationQueryFilter)
    {
        var clientId = await ResolveMyClientId();
        if (clientId is null)
        {
            return NoLinkedClientResponse();
        }

        var (pageSize, pageNumber) = ResolvePaging(paginationQueryFilter);

        var query =
            from i in _unitOfWork.Repository<Installment>().Query()
            join a in _unitOfWork.Repository<AccountReceivable>().Query() on i.AccountReceivableId equals a.AccountReceivableId
            where a.ClientId == clientId.Value
            orderby i.DueDate
            select i;

        var totalRecords = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<InstallmentDto>
            {
                Items = items.Select(i => new InstallmentDto
                {
                    InstallmentId = i.InstallmentId,
                    AccountReceivableId = i.AccountReceivableId,
                    InstallmentNumber = i.InstallmentNumber,
                    InstallmentAmount = i.InstallmentAmount,
                    DueDate = i.DueDate,
                    Status = i.Status
                }),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetMyPayments(PaginationQueryFilter paginationQueryFilter)
    {
        var clientId = await ResolveMyClientId();
        if (clientId is null)
        {
            return NoLinkedClientResponse();
        }

        var (pageSize, pageNumber) = ResolvePaging(paginationQueryFilter);

        var query =
            from p in _unitOfWork.Repository<Payment>().Query()
            join a in _unitOfWork.Repository<AccountReceivable>().Query() on p.AccountReceivableId equals a.AccountReceivableId
            where a.ClientId == clientId.Value
            orderby p.PaymentDate descending
            select p;

        var totalRecords = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<PaymentDto>
            {
                Items = items.Select(p => new PaymentDto
                {
                    PaymentId = p.PaymentId,
                    AccountReceivableId = p.AccountReceivableId,
                    InstallmentId = p.InstallmentId,
                    Amount = p.Amount,
                    PaymentDate = p.PaymentDate,
                    PaymentMethod = p.PaymentMethod,
                    Notes = p.Notes,
                    CreatedById = p.CreatedById
                }),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetMyIssues(PaginationQueryFilter paginationQueryFilter, PortalDateRangeQueryFilter dateRangeQueryFilter)
    {
        var clientId = await ResolveMyClientId();
        if (clientId is null)
        {
            return NoLinkedClientResponse();
        }

        var (pageSize, pageNumber) = ResolvePaging(paginationQueryFilter);

        var query = _unitOfWork.Repository<Issue>().Query()
            .Where(i => i.ClientId == clientId.Value);

        if (dateRangeQueryFilter.DateFrom.HasValue)
        {
            var dateFrom = dateRangeQueryFilter.DateFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(i => i.IssueDate >= dateFrom);
        }

        if (dateRangeQueryFilter.DateTo.HasValue)
        {
            var dateTo = dateRangeQueryFilter.DateTo.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(i => i.IssueDate <= dateTo);
        }

        query = query.OrderByDescending(i => i.IssueDate);

        var totalRecords = await query.CountAsync();
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync();

        var issueIds = items.Select(i => i.IssueId).ToList();
        var totalsByIssueId = await _unitOfWork.Repository<IssueDetail>().Query()
            .Where(d => issueIds.Contains(d.IssueId))
            .GroupBy(d => d.IssueId)
            .Select(g => new { IssueId = g.Key, Total = g.Sum(d => d.TotalCost) })
            .ToDictionaryAsync(g => g.IssueId, g => g.Total);

        return new ResponseGetObject
        {
            Data = new PagedResult<PortalIssueDto>
            {
                Items = items.Select(i => new PortalIssueDto
                {
                    IssueId = i.IssueId,
                    IssueDate = i.IssueDate,
                    Total = totalsByIssueId.GetValueOrDefault(i.IssueId, 0)
                }),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<long?> ResolveMyClientId()
    {
        var appUserId = _currentUserService.AppUserId;
        if (appUserId is null)
        {
            return null;
        }

        var client = await _unitOfWork.Repository<Client>().Query()
            .FirstOrDefaultAsync(c => c.AppUserId == appUserId.Value);

        return client?.ClientId;
    }

    private (int PageSize, int PageNumber) ResolvePaging(PaginationQueryFilter paginationQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;
        return (pageSize, pageNumber);
    }

    private static ResponseGetObject NoLinkedClientResponse() => new()
    {
        Data = new(),
        Messages = [new Message { Type = MessageType.Error, Description = "Your account is not linked to a client profile." }],
        StatusCode = HttpStatusCode.NotFound
    };
}