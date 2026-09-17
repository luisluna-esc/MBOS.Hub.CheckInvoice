using System.Net;
using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Interfaces.Finance;
using CheckInvoice.Application.Interfaces.Security;
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

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<PaymentDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public PaymentService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<PaymentDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllPayments(PaginationQueryFilter paginationQueryFilter, PaymentQueryFilter paymentQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Payment>().Query();

        if (paymentQueryFilter.PaymentId.HasValue)
        {
            query = query.Where(p => p.PaymentId == paymentQueryFilter.PaymentId.Value);
        }

        if (paymentQueryFilter.AccountReceivableId.HasValue)
        {
            query = query.Where(p => p.AccountReceivableId == paymentQueryFilter.AccountReceivableId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paymentQueryFilter.PaymentMethod))
        {
            query = query.Where(p => p.PaymentMethod == paymentQueryFilter.PaymentMethod);
        }

        var totalRecords = await query.CountAsync();

        var payments = await query
            .OrderByDescending(p => p.PaymentDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<PaymentDto>
            {
                Items = payments.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertPayment(PaymentDto paymentDto)
    {
        var validationResult = await _validator.ValidateAsync(paymentDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        var accountReceivableRepository = _unitOfWork.Repository<AccountReceivable>();
        var accountReceivable = await accountReceivableRepository.GetByIdAsync(paymentDto.AccountReceivableId);

        if (accountReceivable is null)
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "AccountReceivableId does not reference an existing account receivable." });
        }

        Installment? installment = null;
        var installmentRepository = _unitOfWork.Repository<Installment>();

        if (paymentDto.InstallmentId.HasValue)
        {
            installment = await installmentRepository.GetByIdAsync(paymentDto.InstallmentId.Value);

            if (installment is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "InstallmentId does not reference an existing installment." });
            }
            else if (installment.AccountReceivableId != paymentDto.AccountReceivableId)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "InstallmentId does not belong to the given AccountReceivableId." });
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

        if (paymentDto.Amount > accountReceivable!.OutstandingBalance)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message
                {
                    Type = MessageType.Error,
                    Description = $"Payment amount exceeds outstanding balance: {accountReceivable.OutstandingBalance} remaining, {paymentDto.Amount} requested."
                }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var payment = new Payment
        {
            AccountReceivableId = paymentDto.AccountReceivableId,
            InstallmentId = paymentDto.InstallmentId,
            Amount = paymentDto.Amount,
            PaymentDate = DateTime.UtcNow,
            PaymentMethod = paymentDto.PaymentMethod,
            Notes = paymentDto.Notes,
            CreatedById = _currentUserService.AppUserId
        };

        await _unitOfWork.Repository<Payment>().AddAsync(payment);

        accountReceivable.OutstandingBalance -= paymentDto.Amount;
        if (accountReceivable.OutstandingBalance <= 0)
        {
            accountReceivable.OutstandingBalance = 0;
            accountReceivable.Status = "paid";
        }
        accountReceivableRepository.Update(accountReceivable);

        if (installment is not null)
        {
            var paidSoFar = await _unitOfWork.Repository<Payment>().Query()
                .Where(p => p.InstallmentId == installment.InstallmentId)
                .SumAsync(p => p.Amount);
            paidSoFar += paymentDto.Amount;

            if (paidSoFar >= installment.InstallmentAmount)
            {
                installment.Status = "paid";
                installmentRepository.Update(installment);
            }
        }

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = payment.PaymentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Payment created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    private static PaymentDto ToDto(Payment payment) => new()
    {
        PaymentId = payment.PaymentId,
        AccountReceivableId = payment.AccountReceivableId,
        InstallmentId = payment.InstallmentId,
        Amount = payment.Amount,
        PaymentDate = payment.PaymentDate,
        PaymentMethod = payment.PaymentMethod,
        Notes = payment.Notes,
        CreatedById = payment.CreatedById
    };
}