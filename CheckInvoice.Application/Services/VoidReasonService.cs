using System.Net;
using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class VoidReasonService : IVoidReasonService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;

    public VoidReasonService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
    }

    public async Task<ResponseGetObject> GetAllVoidReasons(PaginationQueryFilter paginationQueryFilter, VoidReasonQueryFilter voidReasonQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<VoidReason>().Query();

        if (voidReasonQueryFilter.VoidReasonId.HasValue)
        {
            query = query.Where(v => v.VoidReasonId == voidReasonQueryFilter.VoidReasonId.Value);
        }

        var totalRecords = await query.CountAsync();

        var voidReasons = await query
            .OrderBy(v => v.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<VoidReasonDto>
            {
                Items = voidReasons.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static VoidReasonDto ToDto(VoidReason voidReason) => new()
    {
        VoidReasonId = voidReason.VoidReasonId,
        Code = voidReason.Code,
        Name = voidReason.Name
    };
}
