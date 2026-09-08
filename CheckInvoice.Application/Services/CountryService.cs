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

public class CountryService : ICountryService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<CountryDto> _validator;

    public CountryService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<CountryDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllCountries(PaginationQueryFilter paginationQueryFilter, CountryQueryFilter countryQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Country>().Query();

        if (countryQueryFilter.CountryId.HasValue)
        {
            query = query.Where(c => c.CountryId == countryQueryFilter.CountryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(c => c.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var countries = await query
            .OrderBy(c => c.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<CountryDto>
            {
                Items = countries.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertCountry(CountryDto countryDto)
    {
        var validationResult = await _validator.ValidateAsync(countryDto);
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

        var country = new Country
        {
            Name = countryDto.Name
        };

        await _unitOfWork.Repository<Country>().AddAsync(country);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = country.CountryId,
            Messages = [new Message { Type = MessageType.Success, Description = "Country created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateCountry(long id, CountryDto countryDto)
    {
        var validationResult = await _validator.ValidateAsync(countryDto);
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

        var repository = _unitOfWork.Repository<Country>();
        var country = await repository.GetByIdAsync(id);

        if (country is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Country not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        country.Name = countryDto.Name;

        repository.Update(country);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = country.CountryId,
            Messages = [new Message { Type = MessageType.Success, Description = "Country updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteCountry(long id)
    {
        var repository = _unitOfWork.Repository<Country>();
        var country = await repository.GetByIdAsync(id);

        if (country is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Country not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(country);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = country.CountryId,
            Messages = [new Message { Type = MessageType.Success, Description = "Country deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static CountryDto ToDto(Country country) => new()
    {
        CountryId = country.CountryId,
        Name = country.Name
    };
}