using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Finance;
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

public class DiscountService : IDiscountService
{
    private static readonly string[] ValidStatusTransitions = ["approved", "rejected"];

    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<DiscountDto> _validator;

    public DiscountService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<DiscountDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllDiscounts(PaginationQueryFilter paginationQueryFilter, DiscountQueryFilter discountQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Discount>().Query();

        if (discountQueryFilter.DiscountId.HasValue)
        {
            query = query.Where(d => d.DiscountId == discountQueryFilter.DiscountId.Value);
        }

        if (discountQueryFilter.ClientId.HasValue)
        {
            query = query.Where(d => d.ClientId == discountQueryFilter.ClientId.Value);
        }

        if (!string.IsNullOrWhiteSpace(discountQueryFilter.SourceTable))
        {
            query = query.Where(d => d.SourceTable == discountQueryFilter.SourceTable);
        }

        if (!string.IsNullOrWhiteSpace(discountQueryFilter.Status))
        {
            query = query.Where(d => d.Status == discountQueryFilter.Status);
        }

        var totalRecords = await query.CountAsync();

        var discounts = await query
            .OrderByDescending(d => d.DiscountDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<DiscountDto>
            {
                Items = discounts.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertDiscount(DiscountDto discountDto)
    {
        var validationResult = await _validator.ValidateAsync(discountDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (discountDto.ClientId.HasValue &&
            !await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == discountDto.ClientId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ClientId does not reference an existing client." });
        }

        if (errors.Count == 0)
        {
            var sourceExists = discountDto.SourceTable switch
            {
                "product" => await _unitOfWork.Repository<Product>().Query().AnyAsync(p => p.ProductId == discountDto.SourceId),
                "account_receivable" => await _unitOfWork.Repository<AccountReceivable>().Query().AnyAsync(a => a.AccountReceivableId == discountDto.SourceId),
                _ => false
            };

            if (!sourceExists)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"SourceId does not reference an existing record in '{discountDto.SourceTable}'." });
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

        var discount = new Discount
        {
            ClientId = discountDto.ClientId,
            SourceTable = discountDto.SourceTable,
            SourceId = discountDto.SourceId,
            Amount = discountDto.Amount,
            Description = discountDto.Description,
            DiscountDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Status = "pending"
        };

        await _unitOfWork.Repository<Discount>().AddAsync(discount);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = discount.DiscountId,
            Messages = [new Message { Type = MessageType.Success, Description = "Discount created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateDiscountStatus(long id, DiscountStatusUpdateDto discountStatusUpdateDto)
    {
        if (!ValidStatusTransitions.Contains(discountStatusUpdateDto.Status))
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"Status must be one of: {string.Join(", ", ValidStatusTransitions)}." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Discount>();
        var discount = await repository.GetByIdAsync(id);

        if (discount is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Discount not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (discount.Status != "pending")
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"Only discounts in 'pending' status can change status. Current status: '{discount.Status}'." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        discount.Status = discountStatusUpdateDto.Status;
        repository.Update(discount);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = discount.DiscountId,
            Messages = [new Message { Type = MessageType.Success, Description = $"Discount {discountStatusUpdateDto.Status} successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static DiscountDto ToDto(Discount discount) => new()
    {
        DiscountId = discount.DiscountId,
        ClientId = discount.ClientId,
        SourceTable = discount.SourceTable,
        SourceId = discount.SourceId,
        Amount = discount.Amount,
        Description = discount.Description,
        DiscountDate = discount.DiscountDate,
        Status = discount.Status
    };
}