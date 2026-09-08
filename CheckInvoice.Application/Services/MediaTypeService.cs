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

public class MediaTypeService : IMediaTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<MediaTypeDto> _validator;

    public MediaTypeService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<MediaTypeDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllMediaTypes(PaginationQueryFilter paginationQueryFilter, MediaTypeQueryFilter mediaTypeQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<MediaType>().Query();

        if (mediaTypeQueryFilter.MediaTypeId.HasValue)
        {
            query = query.Where(m => m.MediaTypeId == mediaTypeQueryFilter.MediaTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(m => m.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (mediaTypeQueryFilter.IsActive.HasValue)
        {
            query = query.Where(m => m.IsActive == mediaTypeQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var mediaTypes = await query
            .OrderBy(m => m.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<MediaTypeDto>
            {
                Items = mediaTypes.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertMediaType(MediaTypeDto mediaTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(mediaTypeDto);
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

        var mediaType = new MediaType
        {
            Name = mediaTypeDto.Name,
            IsActive = mediaTypeDto.IsActive
        };

        await _unitOfWork.Repository<MediaType>().AddAsync(mediaType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = mediaType.MediaTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "MediaType created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateMediaType(long id, MediaTypeDto mediaTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(mediaTypeDto);
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

        var repository = _unitOfWork.Repository<MediaType>();
        var mediaType = await repository.GetByIdAsync(id);

        if (mediaType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "MediaType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        mediaType.Name = mediaTypeDto.Name;
        mediaType.IsActive = mediaTypeDto.IsActive;

        repository.Update(mediaType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = mediaType.MediaTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "MediaType updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteMediaType(long id)
    {
        var repository = _unitOfWork.Repository<MediaType>();
        var mediaType = await repository.GetByIdAsync(id);

        if (mediaType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "MediaType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(mediaType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = mediaType.MediaTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "MediaType deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static MediaTypeDto ToDto(MediaType mediaType) => new()
    {
        MediaTypeId = mediaType.MediaTypeId,
        Name = mediaType.Name,
        IsActive = mediaType.IsActive
    };
}