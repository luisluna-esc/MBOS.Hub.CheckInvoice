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

public class ProvinceService : IProvinceService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ProvinceDto> _validator;

    public ProvinceService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ProvinceDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllProvinces(PaginationQueryFilter paginationQueryFilter, ProvinceQueryFilter provinceQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Province>().Query();

        if (provinceQueryFilter.ProvinceId.HasValue)
        {
            query = query.Where(p => p.ProvinceId == provinceQueryFilter.ProvinceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(p => p.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (provinceQueryFilter.IsActive.HasValue)
        {
            query = query.Where(p => p.IsActive == provinceQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var provinces = await query
            .OrderBy(p => p.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ProvinceDto>
            {
                Items = provinces.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertProvince(ProvinceDto provinceDto)
    {
        var validationResult = await _validator.ValidateAsync(provinceDto);
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

        var province = new Province
        {
            Name = provinceDto.Name,
            IsActive = provinceDto.IsActive
        };

        await _unitOfWork.Repository<Province>().AddAsync(province);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = province.ProvinceId,
            Messages = [new Message { Type = MessageType.Success, Description = "Province created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateProvince(long id, ProvinceDto provinceDto)
    {
        var validationResult = await _validator.ValidateAsync(provinceDto);
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

        var repository = _unitOfWork.Repository<Province>();
        var province = await repository.GetByIdAsync(id);

        if (province is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Province not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        province.Name = provinceDto.Name;
        province.IsActive = provinceDto.IsActive;

        repository.Update(province);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = province.ProvinceId,
            Messages = [new Message { Type = MessageType.Success, Description = "Province updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteProvince(long id)
    {
        var repository = _unitOfWork.Repository<Province>();
        var province = await repository.GetByIdAsync(id);

        if (province is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Province not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(province);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = province.ProvinceId,
            Messages = [new Message { Type = MessageType.Success, Description = "Province deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ProvinceDto ToDto(Province province) => new()
    {
        ProvinceId = province.ProvinceId,
        Name = province.Name,
        IsActive = province.IsActive
    };
}