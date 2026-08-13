using System.Net;
using CheckInvoice.Application.Dtos.Warehouses;
using CheckInvoice.Application.Interfaces.Warehouses;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Pagination;
using CheckInvoice.core.QueryFilters.Warehouses;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class StockService : IStockService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;

    public StockService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
    }

    public async Task<ResponseGetObject> GetAllStocks(PaginationQueryFilter paginationQueryFilter, StockQueryFilter stockQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Stock>().Query();

        if (stockQueryFilter.WarehouseId.HasValue)
        {
            query = query.Where(s => s.WarehouseId == stockQueryFilter.WarehouseId.Value);
        }

        if (stockQueryFilter.ProductId.HasValue)
        {
            query = query.Where(s => s.ProductId == stockQueryFilter.ProductId.Value);
        }

        var totalRecords = await query.CountAsync();

        var stocks = await query
            .OrderBy(s => s.WarehouseId).ThenBy(s => s.ProductId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<StockDto>
            {
                Items = stocks.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static StockDto ToDto(Stock stock) => new()
    {
        StockId = stock.StockId,
        WarehouseId = stock.WarehouseId,
        ProductId = stock.ProductId,
        Quantity = stock.Quantity,
        AverageCost = stock.AverageCost
    };
}