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

public class DocumentTypeService : IDocumentTypeService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<DocumentTypeDto> _validator;

    public DocumentTypeService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<DocumentTypeDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllDocumentTypes(PaginationQueryFilter paginationQueryFilter, DocumentTypeQueryFilter documentTypeQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<DocumentType>().Query();

        if (documentTypeQueryFilter.DocumentTypeId.HasValue)
        {
            query = query.Where(d => d.DocumentTypeId == documentTypeQueryFilter.DocumentTypeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(d => d.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var documentTypes = await query
            .OrderBy(d => d.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<DocumentTypeDto>
            {
                Items = documentTypes.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertDocumentType(DocumentTypeDto documentTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(documentTypeDto);
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

        var documentType = new DocumentType
        {
            Name = documentTypeDto.Name
        };

        await _unitOfWork.Repository<DocumentType>().AddAsync(documentType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = documentType.DocumentTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "DocumentType created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateDocumentType(long id, DocumentTypeDto documentTypeDto)
    {
        var validationResult = await _validator.ValidateAsync(documentTypeDto);
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

        var repository = _unitOfWork.Repository<DocumentType>();
        var documentType = await repository.GetByIdAsync(id);

        if (documentType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "DocumentType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        documentType.Name = documentTypeDto.Name;

        repository.Update(documentType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = documentType.DocumentTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "DocumentType updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteDocumentType(long id)
    {
        var repository = _unitOfWork.Repository<DocumentType>();
        var documentType = await repository.GetByIdAsync(id);

        if (documentType is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "DocumentType not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(documentType);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = documentType.DocumentTypeId,
            Messages = [new Message { Type = MessageType.Success, Description = "DocumentType deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static DocumentTypeDto ToDto(DocumentType documentType) => new()
    {
        DocumentTypeId = documentType.DocumentTypeId,
        Name = documentType.Name
    };
}