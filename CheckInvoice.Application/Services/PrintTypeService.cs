using System.Net;
using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class PrintTypeService : IPrintTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<PrintTypeDto> _validator;

    public PrintTypeService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<PrintTypeDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllPrintTypes(PaginationQueryFilter paginationQueryFilter, PrintTypeQueryFilter printTypeQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<PrintType>().Query();

        if (printTypeQueryFilter.PrintTypeId.HasValue)
        {
            query = query.Where(p => p.PrintTypeId == printTypeQueryFilter.PrintTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(p => p.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var printTypes = await query
            .OrderBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<PrintTypeDto>
            {
                Items = printTypes.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertPrintType(PrintTypeDto printTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(printTypeDto);
        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var printType = new PrintType
        {
            Name = printTypeDto.Name
        };

        await _unitOfWork.Repository<PrintType>().AddAsync(printType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = printType.PrintTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "PrintType created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdatePrintType(long id, PrintTypeDto printTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(printTypeDto);
        if (!validationResult.IsValid)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = validationResult.Errors
                    .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
                    .ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<PrintType>();
        var printType = await repository.GetByIdAsync(id);

        if (printType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "PrintType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        printType.Name = printTypeDto.Name;

        repository.Update(printType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = printType.PrintTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "PrintType updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeletePrintType(long id)
    {
        var repository = _unitOfWork.Repository<PrintType>();
        var printType = await repository.GetByIdAsync(id);

        if (printType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "PrintType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(printType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = printType.PrintTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "PrintType deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static PrintTypeDto ToDto(PrintType printType) => new()
    {
        PrintTypeId = printType.PrintTypeId,
        Name = printType.Name
    };
}