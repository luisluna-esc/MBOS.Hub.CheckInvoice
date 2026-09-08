using System.Net;
using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Dtos.StoredProcedures;
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

    // EF Core mapea las columnas del resultado de FromSql/SqlQueryRaw por el nombre exacto
    // de la propiedad C# (ej. "ClientId"), no por el nombre de columna en snake_case que
    // devuelve la función SQL (ej. client_id) — de ahí los alias explícitos.
    private const string GetAccountReceivablesSql = """
        SELECT
            account_receivable_id AS "AccountReceivableId",
            issue_id AS "IssueId",
            issue_date AS "IssueDate",
            client_id AS "ClientId",
            total_amount AS "TotalAmount",
            outstanding_balance AS "OutstandingBalance",
            payment_type AS "PaymentType",
            payment_detail AS "PaymentDetail",
            due_date AS "DueDate",
            status AS "Status",
            created_at AS "CreatedAt",
            created_by_id AS "CreatedById",
            total_records AS "TotalRecords"
        FROM sp_get_account_receivables({0}::bigint, {1}::bigint, {2}::varchar, {3}::varchar, {4}::int, {5}::int)
        """;

    public async Task<ResponseGetObject> GetAllAccountReceivables(PaginationQueryFilter paginationQueryFilter, AccountReceivableQueryFilter accountReceivableQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        // Piloto de rendimiento: antes esto hacía 2 consultas separadas (página + fecha de la
        // salida vinculada). Ahora es una sola consulta con LEFT JOIN via
        // sp_get_account_receivables (Scrips/storedProcedures.sql).
        var rows = await _unitOfWork.SqlQueryAsync<AccountReceivableGetAllRow>(
            GetAccountReceivablesSql,
            (object?)accountReceivableQueryFilter.AccountReceivableId ?? DBNull.Value,
            (object?)accountReceivableQueryFilter.ClientId ?? DBNull.Value,
            (object?)accountReceivableQueryFilter.PaymentType ?? DBNull.Value,
            (object?)accountReceivableQueryFilter.Status ?? DBNull.Value,
            pageNumber,
            pageSize);

        var totalRecords = rows.Count > 0 ? rows[0].TotalRecords : 0;

        // Mismo motivo que en IssueService: SqlQueryRaw no pasa por el ValueConverter global
        // de AppDbContext que marca todo DateTime como Utc al leer.
        foreach (var row in rows)
        {
            row.CreatedAt = DateTime.SpecifyKind(row.CreatedAt, DateTimeKind.Utc);
            if (row.IssueDate.HasValue)
            {
                row.IssueDate = DateTime.SpecifyKind(row.IssueDate.Value, DateTimeKind.Utc);
            }
        }

        return new ResponseGetObject
        {
            Data = new PagedResult<AccountReceivableDto>
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
            PaymentDetail = accountReceivableDto.PaymentDetail,
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

}