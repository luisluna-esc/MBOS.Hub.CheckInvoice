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

public class ChurchTypeService : IChurchTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ChurchTypeDto> _validator;

    public ChurchTypeService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ChurchTypeDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllChurchTypes(PaginationQueryFilter paginationQueryFilter, ChurchTypeQueryFilter churchTypeQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<ChurchType>().Query();

        if (churchTypeQueryFilter.ChurchTypeId.HasValue)
        {
            query = query.Where(c => c.ChurchTypeId == churchTypeQueryFilter.ChurchTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(c => c.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var churchTypes = await query
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ChurchTypeDto>
            {
                Items = churchTypes.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertChurchType(ChurchTypeDto churchTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(churchTypeDto);
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

        var churchType = new ChurchType
        {
            Name = churchTypeDto.Name
        };

        await _unitOfWork.Repository<ChurchType>().AddAsync(churchType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = churchType.ChurchTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "ChurchType created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateChurchType(long id, ChurchTypeDto churchTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(churchTypeDto);
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

        var repository = _unitOfWork.Repository<ChurchType>();
        var churchType = await repository.GetByIdAsync(id);

        if (churchType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "ChurchType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        churchType.Name = churchTypeDto.Name;

        repository.Update(churchType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = churchType.ChurchTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "ChurchType updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteChurchType(long id)
    {
        var repository = _unitOfWork.Repository<ChurchType>();
        var churchType = await repository.GetByIdAsync(id);

        if (churchType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "ChurchType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(churchType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = churchType.ChurchTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "ChurchType deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ChurchTypeDto ToDto(ChurchType churchType) => new()
    {
        ChurchTypeId = churchType.ChurchTypeId,
        Name = churchType.Name
    };
}