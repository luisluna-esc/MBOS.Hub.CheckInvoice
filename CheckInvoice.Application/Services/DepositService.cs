using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class DepositService : IDepositService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<DepositDto> _validator;

    public DepositService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<DepositDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllDeposits(PaginationQueryFilter paginationQueryFilter, DepositQueryFilter depositQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Deposit>().Query();

        if (depositQueryFilter.DepositId.HasValue)
        {
            query = query.Where(d => d.DepositId == depositQueryFilter.DepositId.Value);
        }

        if (depositQueryFilter.ClientId.HasValue)
        {
            query = query.Where(d => d.ClientId == depositQueryFilter.ClientId.Value);
        }

        if (depositQueryFilter.ProductId.HasValue)
        {
            query = query.Where(d => d.ProductId == depositQueryFilter.ProductId.Value);
        }

        if (depositQueryFilter.ShipmentId.HasValue)
        {
            query = query.Where(d => d.ShipmentId == depositQueryFilter.ShipmentId.Value);
        }

        if (!string.IsNullOrWhiteSpace(depositQueryFilter.ReceiptNumber))
        {
            query = query.Where(d => d.ReceiptNumber == depositQueryFilter.ReceiptNumber);
        }

        var totalRecords = await query.CountAsync();

        var deposits = await query
            .OrderByDescending(d => d.DepositDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<DepositDto>
            {
                Items = deposits.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertDeposit(DepositDto depositDto)
    {
        var validationResult = await _validator.ValidateAsync(depositDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (depositDto.ClientId.HasValue &&
            !await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == depositDto.ClientId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ClientId does not reference an existing client." });
        }

        if (depositDto.ProductId.HasValue &&
            !await _unitOfWork.Repository<Product>().Query().AnyAsync(p => p.ProductId == depositDto.ProductId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ProductId does not reference an existing product." });
        }

        if (depositDto.ShipmentId.HasValue &&
            !await _unitOfWork.Repository<Shipment>().Query().AnyAsync(s => s.ShipmentId == depositDto.ShipmentId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ShipmentId does not reference an existing shipment." });
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

        var deposit = new Deposit
        {
            ClientId = depositDto.ClientId,
            ProductId = depositDto.ProductId,
            ShipmentId = depositDto.ShipmentId,
            ReceiptNumber = depositDto.ReceiptNumber,
            DepositDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Amount = depositDto.Amount,
            Notes = depositDto.Notes
        };

        await _unitOfWork.Repository<Deposit>().AddAsync(deposit);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = deposit.DepositId,
            Messages = [new Message { Type = MessageType.Success, Description = "Deposit created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    private static DepositDto ToDto(Deposit deposit) => new()
    {
        DepositId = deposit.DepositId,
        ClientId = deposit.ClientId,
        ProductId = deposit.ProductId,
        ShipmentId = deposit.ShipmentId,
        ReceiptNumber = deposit.ReceiptNumber,
        DepositDate = deposit.DepositDate,
        Amount = deposit.Amount,
        Notes = deposit.Notes
    };
}