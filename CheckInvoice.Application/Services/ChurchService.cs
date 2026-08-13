using System.Net;
using CheckInvoice.Application.Dtos.Organization;
using CheckInvoice.Application.Interfaces.Organization;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Organization;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Organization;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class ChurchService : IChurchService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ChurchDto> _validator;

    public ChurchService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ChurchDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllChurches(PaginationQueryFilter paginationQueryFilter, ChurchQueryFilter churchQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Church>().Query();

        if (churchQueryFilter.ChurchId.HasValue)
        {
            query = query.Where(c => c.ChurchId == churchQueryFilter.ChurchId.Value);
        }

        if (!string.IsNullOrWhiteSpace(churchQueryFilter.Code))
        {
            query = query.Where(c => c.Code == churchQueryFilter.Code);
        }

        if (!string.IsNullOrWhiteSpace(churchQueryFilter.ChurchName))
        {
            query = query.Where(c => c.ChurchName == churchQueryFilter.ChurchName);
        }

        if (churchQueryFilter.DistrictId.HasValue)
        {
            query = query.Where(c => c.DistrictId == churchQueryFilter.DistrictId.Value);
        }

        if (churchQueryFilter.ChurchTypeId.HasValue)
        {
            query = query.Where(c => c.ChurchTypeId == churchQueryFilter.ChurchTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(c => c.ChurchName.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (churchQueryFilter.IsActive.HasValue)
        {
            query = query.Where(c => c.IsActive == churchQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var churches = await query
            .OrderBy(c => c.ChurchName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ChurchDto>
            {
                Items = churches.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertChurch(ChurchDto churchDto)
    {
        var validationResult = await _validator.ValidateAsync(churchDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(churchDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var church = new Church
        {
            Code = churchDto.Code,
            ChurchName = churchDto.ChurchName,
            DistrictId = churchDto.DistrictId,
            ChurchTypeId = churchDto.ChurchTypeId,
            IsActive = churchDto.IsActive
        };

        await _unitOfWork.Repository<Church>().AddAsync(church);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = church.ChurchId,
            Messages = [new Message { Type = MessageType.Success, Description = "Church created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateChurch(long id, ChurchDto churchDto)
    {
        var validationResult = await _validator.ValidateAsync(churchDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(churchDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Church>();
        var church = await repository.GetByIdAsync(id);

        if (church is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Church not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        church.Code = churchDto.Code;
        church.ChurchName = churchDto.ChurchName;
        church.DistrictId = churchDto.DistrictId;
        church.ChurchTypeId = churchDto.ChurchTypeId;
        church.IsActive = churchDto.IsActive;

        repository.Update(church);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = church.ChurchId,
            Messages = [new Message { Type = MessageType.Success, Description = "Church updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteChurch(long id)
    {
        var repository = _unitOfWork.Repository<Church>();
        var church = await repository.GetByIdAsync(id);

        if (church is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Church not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(church);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = church.ChurchId,
            Messages = [new Message { Type = MessageType.Success, Description = "Church deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task ValidateForeignKeys(ChurchDto churchDto, List<Message> errors)
    {
        if (churchDto.DistrictId.HasValue &&
            !await _unitOfWork.Repository<District>().Query().AnyAsync(d => d.DistrictId == churchDto.DistrictId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "DistrictId does not reference an existing district." });
        }

        if (churchDto.ChurchTypeId.HasValue &&
            !await _unitOfWork.Repository<ChurchType>().Query().AnyAsync(c => c.ChurchTypeId == churchDto.ChurchTypeId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ChurchTypeId does not reference an existing church type." });
        }
    }

    private static ChurchDto ToDto(Church church) => new()
    {
        ChurchId = church.ChurchId,
        Code = church.Code,
        ChurchName = church.ChurchName,
        DistrictId = church.DistrictId,
        ChurchTypeId = church.ChurchTypeId,
        IsActive = church.IsActive
    };
}