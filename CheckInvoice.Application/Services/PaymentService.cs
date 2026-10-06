using System.Globalization;
using System.Net;
using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Interfaces.Finance;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Products;
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
    private static readonly CultureInfo MoneyCulture = new("es-ES");

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
            errors.Add(new Message { Type = MessageType.Error, Description = "La cuenta por cobrar seleccionada no existe." });
        }

        Installment? installment = null;
        var installmentRepository = _unitOfWork.Repository<Installment>();

        if (paymentDto.InstallmentId.HasValue)
        {
            installment = await installmentRepository.GetByIdAsync(paymentDto.InstallmentId.Value);

            if (installment is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "La cuota seleccionada no existe." });
            }
            else if (installment.AccountReceivableId != paymentDto.AccountReceivableId)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "La cuota seleccionada no pertenece a esta cuenta por cobrar." });
            }
        }

        // Con la fecha límite vencida la cuenta pasa a "Pago retrasado" (mismo criterio que
        // sp_get_account_receivables) y ya no se cobra por Caja: se descuenta del sueldo.
        if (accountReceivable is not null && AccountReceivableStatus.IsLate(accountReceivable.Status, accountReceivable.DueDate))
        {
            errors.Add(new Message
            {
                Type = MessageType.Error,
                Description = "Esta cuenta tiene pago retrasado: ya no se registran depósitos, se descontará del sueldo."
            });
        }

        // Depósito por producto: cada monto se asigna a una línea de la salida y no puede pasar
        // de lo que falta pagar de esa línea. Sin salida (cuenta creada a mano) no hay productos
        // y el depósito es un monto general, como antes.
        var paymentDetails = new List<PaymentDetail>();
        if (accountReceivable is not null && errors.Count == 0)
        {
            if (accountReceivable.IssueId.HasValue)
            {
                if (paymentDto.Details.Count == 0)
                {
                    errors.Add(new Message { Type = MessageType.Error, Description = "Indica cuánto se deposita de al menos un producto." });
                }
                else
                {
                    var lines = (await GetLinesForIssueAsync(accountReceivable.IssueId.Value))
                        .ToDictionary(l => l.IssueDetailId);

                    foreach (var detail in paymentDto.Details)
                    {
                        if (!lines.TryGetValue(detail.IssueDetailId, out var line))
                        {
                            errors.Add(new Message { Type = MessageType.Error, Description = "Uno de los productos no pertenece a la salida de esta cuenta." });
                        }
                        else if (detail.Amount > line.RemainingAmount)
                        {
                            errors.Add(new Message
                            {
                                Type = MessageType.Error,
                                Description = $"{line.Name}: solo faltan {line.RemainingAmount.ToString("N2", MoneyCulture)} Bs."
                            });
                        }
                        else
                        {
                            paymentDetails.Add(new PaymentDetail
                            {
                                IssueDetailId = line.IssueDetailId,
                                ProductId = line.ProductId,
                                Amount = detail.Amount
                            });
                        }
                    }

                    paymentDto.Amount = paymentDto.Details.Sum(d => d.Amount);
                }
            }
            else if (paymentDto.Details.Count > 0)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "Esta cuenta no tiene productos: registra el monto total del depósito." });
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
                    Description = $"El pago supera el saldo pendiente: quedan {accountReceivable.OutstandingBalance}, se intentó registrar {paymentDto.Amount}."
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
            CreatedById = _currentUserService.AppUserId,
            Details = paymentDetails
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
            Messages = [new Message { Type = MessageType.Success, Description = "Pago registrado correctamente." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponseGetObject> GetPaymentLines(long accountReceivableId)
    {
        var accountReceivable = await _unitOfWork.Repository<AccountReceivable>().GetByIdAsync(accountReceivableId);
        if (accountReceivable is null)
        {
            return new ResponseGetObject
            {
                Data = null,
                Messages = [new Message { Type = MessageType.Error, Description = "La cuenta por cobrar seleccionada no existe." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var lines = accountReceivable.IssueId.HasValue
            ? await GetLinesForIssueAsync(accountReceivable.IssueId.Value)
            : [];

        return new ResponseGetObject
        {
            Data = new PaymentLinesDto
            {
                AccountReceivableId = accountReceivable.AccountReceivableId,
                OutstandingBalance = accountReceivable.OutstandingBalance,
                HasProducts = lines.Count > 0,
                Lines = lines
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<List<PaymentLineDto>> GetLinesForIssueAsync(long issueId) =>
        (await ReceivableLines.ForIssuesAsync(_unitOfWork, [issueId])).GetValueOrDefault(issueId, []);

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