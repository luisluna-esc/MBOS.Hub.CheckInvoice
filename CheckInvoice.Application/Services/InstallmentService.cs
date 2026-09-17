using System.Net;
using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Interfaces.Finance;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Finance;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class InstallmentService : IInstallmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<InstallmentDto> _validator;

    public InstallmentService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<InstallmentDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllInstallments(PaginationQueryFilter paginationQueryFilter, InstallmentQueryFilter installmentQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Installment>().Query();

        if (installmentQueryFilter.InstallmentId.HasValue)
        {
            query = query.Where(i => i.InstallmentId == installmentQueryFilter.InstallmentId.Value);
        }

        if (installmentQueryFilter.AccountReceivableId.HasValue)
        {
            query = query.Where(i => i.AccountReceivableId == installmentQueryFilter.AccountReceivableId.Value);
        }

        if (!string.IsNullOrWhiteSpace(installmentQueryFilter.Status))
        {
            query = query.Where(i => i.Status == installmentQueryFilter.Status);
        }

        var totalRecords = await query.CountAsync();

        var installments = await query
            .OrderBy(i => i.DueDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<InstallmentDto>
            {
                Items = installments.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertInstallment(InstallmentDto installmentDto)
    {
        var validationResult = await _validator.ValidateAsync(installmentDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (!await _unitOfWork.Repository<AccountReceivable>().Query().AnyAsync(a => a.AccountReceivableId == installmentDto.AccountReceivableId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "AccountReceivableId does not reference an existing account receivable." });
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

        var installment = new Installment
        {
            AccountReceivableId = installmentDto.AccountReceivableId,
            InstallmentNumber = installmentDto.InstallmentNumber,
            InstallmentAmount = installmentDto.InstallmentAmount,
            DueDate = installmentDto.DueDate,
            Status = "pending"
        };

        await _unitOfWork.Repository<Installment>().AddAsync(installment);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = installment.InstallmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Installment created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    private static InstallmentDto ToDto(Installment installment) => new()
    {
        InstallmentId = installment.InstallmentId,
        AccountReceivableId = installment.AccountReceivableId,
        InstallmentNumber = installment.InstallmentNumber,
        InstallmentAmount = installment.InstallmentAmount,
        DueDate = installment.DueDate,
        Status = installment.Status
    };
}