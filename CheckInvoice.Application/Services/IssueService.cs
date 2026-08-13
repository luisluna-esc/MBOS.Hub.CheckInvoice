using System.Net;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Movements;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class IssueService : IIssueService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<IssueRequestDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public IssueService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<IssueRequestDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllIssues(PaginationQueryFilter paginationQueryFilter, IssueQueryFilter issueQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Issue>().Query();

        if (issueQueryFilter.IssueId.HasValue)
        {
            query = query.Where(i => i.IssueId == issueQueryFilter.IssueId.Value);
        }

        if (issueQueryFilter.ClientId.HasValue)
        {
            query = query.Where(i => i.ClientId == issueQueryFilter.ClientId.Value);
        }

        if (issueQueryFilter.WarehouseId.HasValue)
        {
            query = query.Where(i => i.WarehouseId == issueQueryFilter.WarehouseId.Value);
        }

        if (issueQueryFilter.IssueTypeId.HasValue)
        {
            query = query.Where(i => i.IssueTypeId == issueQueryFilter.IssueTypeId.Value);
        }

        var totalRecords = await query.CountAsync();

        var issues = await query
            .OrderByDescending(i => i.IssueDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<IssueDto>
            {
                Items = issues.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetIssueDetails(long issueId)
    {
        var issue = await _unitOfWork.Repository<Issue>().GetByIdAsync(issueId);
        if (issue is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Issue not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var details = await _unitOfWork.Repository<IssueDetail>().Query()
            .Where(d => d.IssueId == issueId)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = details.Select(d => new IssueDetailDto
            {
                IssueDetailId = d.IssueDetailId,
                IssueId = d.IssueId,
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitCost = d.UnitCost,
                TotalCost = d.TotalCost
            }),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertIssue(IssueRequestDto issueRequestDto)
    {
        var validationResult = await _validator.ValidateAsync(issueRequestDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (!await _unitOfWork.Repository<Warehouse>().Query().AnyAsync(w => w.WarehouseId == issueRequestDto.WarehouseId))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "WarehouseId does not reference an existing warehouse." });
        }

        if (issueRequestDto.IssueTypeId.HasValue &&
            !await _unitOfWork.Repository<IssueType>().Query().AnyAsync(i => i.IssueTypeId == issueRequestDto.IssueTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "IssueTypeId does not reference an existing issue type." });
        }

        if (issueRequestDto.WarehousePeriodId.HasValue)
        {
            var warehousePeriod = await _unitOfWork.Repository<WarehousePeriod>().Query()
                .FirstOrDefaultAsync(w => w.WarehousePeriodId == issueRequestDto.WarehousePeriodId.Value);

            if (warehousePeriod is null)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "WarehousePeriodId does not reference an existing warehouse period." });
            }
            else if (warehousePeriod.IsClosed)
            {
                errors.Add(new Message { Type = MessageType.Error, Description = "This warehouse period is closed and cannot receive new movements." });
            }
        }

        if (issueRequestDto.ClientId.HasValue &&
            !await _unitOfWork.Repository<Client>().Query().AnyAsync(c => c.ClientId == issueRequestDto.ClientId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ClientId does not reference an existing client." });
        }

        if (issueRequestDto.PrintTypeId.HasValue &&
            !await _unitOfWork.Repository<PrintType>().Query().AnyAsync(p => p.PrintTypeId == issueRequestDto.PrintTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "PrintTypeId does not reference an existing print type." });
        }

        foreach (var line in issueRequestDto.Details)
        {
            if (!await _unitOfWork.Repository<Product>().Query().AnyAsync(p => p.ProductId == line.ProductId))
            {
                errors.Add(new Message { Type = MessageType.Error, Description = $"ProductId {line.ProductId} does not reference an existing product." });
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

        var requestedByProduct = issueRequestDto.Details
            .GroupBy(d => d.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(d => d.Quantity));

        var stockByProduct = new Dictionary<long, Stock>();
        foreach (var productId in requestedByProduct.Keys)
        {
            var stock = await _unitOfWork.Repository<Stock>().Query()
                .FirstOrDefaultAsync(s => s.WarehouseId == issueRequestDto.WarehouseId && s.ProductId == productId);

            var available = stock?.Quantity ?? 0;
            var requested = requestedByProduct[productId];

            if (available < requested)
            {
                errors.Add(new Message
                {
                    Type = MessageType.Error,
                    Description = $"Insufficient stock for ProductId {productId}: {available} available, {requested} requested."
                });
                continue;
            }

            stockByProduct[productId] = stock!;
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

        var issue = new Issue
        {
            IssueTypeId = issueRequestDto.IssueTypeId,
            WarehouseId = issueRequestDto.WarehouseId,
            WarehousePeriodId = issueRequestDto.WarehousePeriodId,
            ClientId = issueRequestDto.ClientId,
            Complement = issueRequestDto.Complement,
            IssueDate = DateTime.UtcNow,
            PrintTypeId = issueRequestDto.PrintTypeId,
            Description = issueRequestDto.Description
        };

        await _unitOfWork.Repository<Issue>().AddAsync(issue);
        await _unitOfWork.SaveChangesAsync();

        var stockRepository = _unitOfWork.Repository<Stock>();

        foreach (var line in issueRequestDto.Details)
        {
            var stock = stockByProduct[line.ProductId];
            var unitCost = stock.AverageCost;

            await _unitOfWork.Repository<IssueDetail>().AddAsync(new IssueDetail
            {
                IssueId = issue.IssueId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UnitCost = unitCost,
                TotalCost = line.Quantity * unitCost
            });

            stock.Quantity -= line.Quantity;
        }

        foreach (var stock in stockByProduct.Values)
        {
            stockRepository.Update(stock);
        }

        await _unitOfWork.SaveChangesAsync();

        if (issueRequestDto.SendToAccountsReceivable)
        {
            var issueDetails = await _unitOfWork.Repository<IssueDetail>().Query()
                .Where(d => d.IssueId == issue.IssueId)
                .ToListAsync();

            var totalAmount = issueDetails.Sum(d => d.TotalCost);

            var accountReceivable = new AccountReceivable
            {
                IssueId = issue.IssueId,
                ClientId = issueRequestDto.ClientId,
                TotalAmount = totalAmount,
                OutstandingBalance = totalAmount,
                PaymentType = issueRequestDto.PaymentType!,
                DueDate = issueRequestDto.DueDate,
                Status = "pending",
                CreatedAt = DateTime.UtcNow,
                CreatedById = _currentUserService.AppUserId
            };

            await _unitOfWork.Repository<AccountReceivable>().AddAsync(accountReceivable);
            await _unitOfWork.SaveChangesAsync();
        }

        return new ResponsePost
        {
            Id = issue.IssueId,
            Messages = [new Message { Type = MessageType.Success, Description = "Issue created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    private static IssueDto ToDto(Issue issue) => new()
    {
        IssueId = issue.IssueId,
        IssueTypeId = issue.IssueTypeId,
        WarehouseId = issue.WarehouseId,
        WarehousePeriodId = issue.WarehousePeriodId,
        ClientId = issue.ClientId,
        Complement = issue.Complement,
        IssueDate = issue.IssueDate,
        PrintTypeId = issue.PrintTypeId,
        Description = issue.Description
    };
}