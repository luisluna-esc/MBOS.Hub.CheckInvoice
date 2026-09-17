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

public class SpecialCaseService : ISpecialCaseService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<SpecialCaseDto> _validator;

    public SpecialCaseService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<SpecialCaseDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllSpecialCases(PaginationQueryFilter paginationQueryFilter, SpecialCaseQueryFilter specialCaseQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<SpecialCase>().Query();

        if (specialCaseQueryFilter.SpecialCaseId.HasValue)
        {
            query = query.Where(s => s.SpecialCaseId == specialCaseQueryFilter.SpecialCaseId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(s => s.Name.Contains(paginationQueryFilter.SearchCriteria) || s.Code.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var specialCases = await query
            .OrderBy(s => s.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<SpecialCaseDto>
            {
                Items = specialCases.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertSpecialCase(SpecialCaseDto specialCaseDto)
    {
        var validationResult = await _validator.ValidateAsync(specialCaseDto);
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

        var specialCase = new SpecialCase
        {
            Code = specialCaseDto.Code,
            Name = specialCaseDto.Name
        };

        await _unitOfWork.Repository<SpecialCase>().AddAsync(specialCase);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = specialCase.SpecialCaseId,
            Messages = [new Message { Type = MessageType.Success, Description = "SpecialCase created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateSpecialCase(long id, SpecialCaseDto specialCaseDto)
    {
        var validationResult = await _validator.ValidateAsync(specialCaseDto);
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

        var repository = _unitOfWork.Repository<SpecialCase>();
        var specialCase = await repository.GetByIdAsync(id);

        if (specialCase is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "SpecialCase not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        specialCase.Code = specialCaseDto.Code;
        specialCase.Name = specialCaseDto.Name;

        repository.Update(specialCase);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = specialCase.SpecialCaseId,
            Messages = [new Message { Type = MessageType.Success, Description = "SpecialCase updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteSpecialCase(long id)
    {
        var repository = _unitOfWork.Repository<SpecialCase>();
        var specialCase = await repository.GetByIdAsync(id);

        if (specialCase is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "SpecialCase not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(specialCase);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = specialCase.SpecialCaseId,
            Messages = [new Message { Type = MessageType.Success, Description = "SpecialCase deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static SpecialCaseDto ToDto(SpecialCase specialCase) => new()
    {
        SpecialCaseId = specialCase.SpecialCaseId,
        Code = specialCase.Code,
        Name = specialCase.Name
    };
}
