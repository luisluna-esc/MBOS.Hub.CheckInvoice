using System.Net;
using CheckInvoice.Application.Dtos.Organization;
using CheckInvoice.Application.Interfaces.Organization;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Organization;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Organization;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class DistrictService : IDistrictService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<DistrictDto> _validator;

    public DistrictService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<DistrictDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllDistricts(PaginationQueryFilter paginationQueryFilter, DistrictQueryFilter districtQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<District>().Query();

        if (districtQueryFilter.DistrictId.HasValue)
        {
            query = query.Where(d => d.DistrictId == districtQueryFilter.DistrictId.Value);
        }

        if (!string.IsNullOrWhiteSpace(districtQueryFilter.Code))
        {
            query = query.Where(d => d.Code == districtQueryFilter.Code);
        }

        if (!string.IsNullOrWhiteSpace(districtQueryFilter.DistrictName))
        {
            query = query.Where(d => d.DistrictName == districtQueryFilter.DistrictName);
        }

        if (districtQueryFilter.MissionId.HasValue)
        {
            query = query.Where(d => d.MissionId == districtQueryFilter.MissionId.Value);
        }

        if (districtQueryFilter.ProvinceId.HasValue)
        {
            query = query.Where(d => d.ProvinceId == districtQueryFilter.ProvinceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(d => d.DistrictName.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (districtQueryFilter.IsActive.HasValue)
        {
            query = query.Where(d => d.IsActive == districtQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var districts = await query
            .OrderBy(d => d.DistrictName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<DistrictDto>
            {
                Items = districts.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertDistrict(DistrictDto districtDto)
    {
        var validationResult = await _validator.ValidateAsync(districtDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(districtDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var district = new District
        {
            Code = districtDto.Code,
            DistrictName = districtDto.DistrictName,
            MissionId = districtDto.MissionId,
            ProvinceId = districtDto.ProvinceId,
            AppUserId = districtDto.AppUserId,
            RegistrationDate = DateTime.UtcNow,
            IsActive = districtDto.IsActive
        };

        await _unitOfWork.Repository<District>().AddAsync(district);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = district.DistrictId,
            Messages = [new Message { Type = MessageType.Success, Description = "District created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateDistrict(long id, DistrictDto districtDto)
    {
        var validationResult = await _validator.ValidateAsync(districtDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        await ValidateForeignKeys(districtDto, errors);

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<District>();
        var district = await repository.GetByIdAsync(id);

        if (district is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "District not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        district.Code = districtDto.Code;
        district.DistrictName = districtDto.DistrictName;
        district.MissionId = districtDto.MissionId;
        district.ProvinceId = districtDto.ProvinceId;
        district.AppUserId = districtDto.AppUserId;
        district.IsActive = districtDto.IsActive;

        repository.Update(district);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = district.DistrictId,
            Messages = [new Message { Type = MessageType.Success, Description = "District updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteDistrict(long id)
    {
        var repository = _unitOfWork.Repository<District>();
        var district = await repository.GetByIdAsync(id);

        if (district is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "District not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(district);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = district.DistrictId,
            Messages = [new Message { Type = MessageType.Success, Description = "District deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task ValidateForeignKeys(DistrictDto districtDto, List<Message> errors)
    {
        if (districtDto.MissionId.HasValue &&
            !await _unitOfWork.Repository<Mission>().Query().AnyAsync(m => m.MissionId == districtDto.MissionId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "MissionId does not reference an existing mission." });
        }

        if (districtDto.ProvinceId.HasValue &&
            !await _unitOfWork.Repository<Province>().Query().AnyAsync(p => p.ProvinceId == districtDto.ProvinceId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "ProvinceId does not reference an existing province." });
        }

        if (districtDto.AppUserId.HasValue &&
            !await _unitOfWork.Repository<AppUser>().Query().AnyAsync(u => u.AppUserId == districtDto.AppUserId.Value))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "AppUserId does not reference an existing user." });
        }
    }

    private static DistrictDto ToDto(District district) => new()
    {
        DistrictId = district.DistrictId,
        Code = district.Code,
        DistrictName = district.DistrictName,
        MissionId = district.MissionId,
        ProvinceId = district.ProvinceId,
        AppUserId = district.AppUserId,
        RegistrationDate = district.RegistrationDate,
        IsActive = district.IsActive
    };
}