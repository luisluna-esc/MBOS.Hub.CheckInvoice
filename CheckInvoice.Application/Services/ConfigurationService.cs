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

public class ConfigurationService : IConfigurationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ConfigurationDto> _validator;

    public ConfigurationService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ConfigurationDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllConfigurations(PaginationQueryFilter paginationQueryFilter, ConfigurationQueryFilter configurationQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Configuration>().Query();

        if (configurationQueryFilter.ConfigurationId.HasValue)
        {
            query = query.Where(c => c.ConfigurationId == configurationQueryFilter.ConfigurationId.Value);
        }

        if (!string.IsNullOrWhiteSpace(configurationQueryFilter.Key))
        {
            query = query.Where(c => c.Key == configurationQueryFilter.Key);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(c => c.Key.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var configurations = await query
            .OrderBy(c => c.Key)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ConfigurationDto>
            {
                Items = configurations.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertConfiguration(ConfigurationDto configurationDto)
    {
        var validationResult = await _validator.ValidateAsync(configurationDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (await _unitOfWork.Repository<Configuration>().Query().AnyAsync(c => c.Key == configurationDto.Key))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This key is already in use." });
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

        var configuration = new Configuration
        {
            Key = configurationDto.Key,
            Value = configurationDto.Value,
            Description = configurationDto.Description
        };

        await _unitOfWork.Repository<Configuration>().AddAsync(configuration);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = configuration.ConfigurationId,
            Messages = [new Message { Type = MessageType.Success, Description = "Configuration created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateConfiguration(long id, ConfigurationDto configurationDto)
    {
        var validationResult = await _validator.ValidateAsync(configurationDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (await _unitOfWork.Repository<Configuration>().Query().AnyAsync(c => c.Key == configurationDto.Key && c.ConfigurationId != id))
        {
            errors.Add(new Message { Type = MessageType.Error, Description = "This key is already in use." });
        }

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var repository = _unitOfWork.Repository<Configuration>();
        var configuration = await repository.GetByIdAsync(id);

        if (configuration is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Configuration not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        configuration.Key = configurationDto.Key;
        configuration.Value = configurationDto.Value;
        configuration.Description = configurationDto.Description;

        repository.Update(configuration);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = configuration.ConfigurationId,
            Messages = [new Message { Type = MessageType.Success, Description = "Configuration updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteConfiguration(long id)
    {
        var repository = _unitOfWork.Repository<Configuration>();
        var configuration = await repository.GetByIdAsync(id);

        if (configuration is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Configuration not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(configuration);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = configuration.ConfigurationId,
            Messages = [new Message { Type = MessageType.Success, Description = "Configuration deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ConfigurationDto ToDto(Configuration configuration) => new()
    {
        ConfigurationId = configuration.ConfigurationId,
        Key = configuration.Key,
        Value = configuration.Value,
        Description = configuration.Description
    };
}