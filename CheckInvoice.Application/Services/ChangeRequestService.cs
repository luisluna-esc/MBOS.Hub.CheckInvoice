using System.Net;
using System.Reflection;
using System.Text.Json;
using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.Application.Interfaces.Governance;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Entities.Governance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.ResponseApi.Details;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.Interfaces;
using CheckInvoice.core.QueryFilters.Governance;
using CheckInvoice.core.QueryFilters.Pagination;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CheckInvoice.Application.Services;

public class ChangeRequestService : IChangeRequestService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ChangeRequestCreateDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public ChangeRequestService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<ChangeRequestCreateDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllChangeRequests(PaginationQueryFilter paginationQueryFilter, ChangeRequestQueryFilter changeRequestQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<ChangeRequest>().Query();

        if (changeRequestQueryFilter.ChangeRequestId.HasValue)
        {
            query = query.Where(c => c.ChangeRequestId == changeRequestQueryFilter.ChangeRequestId.Value);
        }

        if (!string.IsNullOrWhiteSpace(changeRequestQueryFilter.TableName))
        {
            query = query.Where(c => c.TableName == changeRequestQueryFilter.TableName);
        }

        if (changeRequestQueryFilter.RecordId.HasValue)
        {
            query = query.Where(c => c.RecordId == changeRequestQueryFilter.RecordId.Value);
        }

        if (!string.IsNullOrWhiteSpace(changeRequestQueryFilter.Status))
        {
            query = query.Where(c => c.Status == changeRequestQueryFilter.Status);
        }

        if (changeRequestQueryFilter.RequestedBy.HasValue)
        {
            query = query.Where(c => c.RequestedBy == changeRequestQueryFilter.RequestedBy.Value);
        }

        var totalRecords = await query.CountAsync();

        var changeRequests = await query
            .OrderByDescending(c => c.RequestedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ChangeRequestDto>
            {
                Items = changeRequests.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponseGetObject> GetChangeRequestById(long id)
    {
        var changeRequest = await _unitOfWork.Repository<ChangeRequest>().Query()
            .FirstOrDefaultAsync(c => c.ChangeRequestId == id);

        if (changeRequest is null)
        {
            return new ResponseGetObject
            {
                Data = new(),
                Messages = [new Message { Type = MessageType.Error, Description = "Change request not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        return new ResponseGetObject
        {
            Data = ToDto(changeRequest),
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> RequestChange(ChangeRequestCreateDto changeRequestCreateDto)
    {
        var validationResult = await _validator.ValidateAsync(changeRequestCreateDto);
        var errors = validationResult.Errors
            .Select(e => new Message { Type = MessageType.Error, Description = e.ErrorMessage })
            .ToList();

        if (errors.Count > 0)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = errors.ToArray(),
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        var tableName = changeRequestCreateDto.TableName.Trim().ToLowerInvariant();
        var action = changeRequestCreateDto.Action.Trim().ToLowerInvariant();

        if (!_currentUserService.HasPermission($"{tableName}.request_change"))
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = $"You do not have permission to request changes on '{tableName}'." }],
                StatusCode = HttpStatusCode.Forbidden
            };
        }

        var entity = await GetHeaderEntitySnapshotAsync(tableName, changeRequestCreateDto.RecordId);

        if (entity is null)
        {
            return new ResponsePost
            {
                Id = 0,
                Messages = [new Message { Type = MessageType.Error, Description = $"No {tableName} found with id {changeRequestCreateDto.RecordId}." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        var currentDataJson = JsonSerializer.Serialize(entity, entity.GetType());
        string? proposedDataJson = null;

        if (action == "edit")
        {
            var applyErrors = ApplyProposedData(entity, changeRequestCreateDto.ProposedData!);

            if (applyErrors.Count > 0)
            {
                return new ResponsePost
                {
                    Id = 0,
                    Messages = applyErrors.Select(e => new Message { Type = MessageType.Error, Description = e }).ToArray(),
                    StatusCode = HttpStatusCode.BadRequest
                };
            }

            proposedDataJson = JsonSerializer.Serialize(changeRequestCreateDto.ProposedData);
        }

        var changeRequest = new ChangeRequest
        {
            TableName = tableName,
            RecordId = changeRequestCreateDto.RecordId,
            Action = action,
            CurrentData = currentDataJson,
            ProposedData = proposedDataJson,
            Reason = changeRequestCreateDto.Reason,
            Status = "pending",
            RequestedBy = _currentUserService.AppUserId!.Value,
            RequestedAt = DateTime.UtcNow
        };

        await _unitOfWork.Repository<ChangeRequest>().AddAsync(changeRequest);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = changeRequest.ChangeRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Change request submitted successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> ApproveChangeRequest(long id, ChangeRequestReviewDto changeRequestReviewDto)
    {
        var repository = _unitOfWork.Repository<ChangeRequest>();
        var changeRequest = await repository.GetByIdAsync(id);

        if (changeRequest is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Change request not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (changeRequest.Status != "pending")
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"Only pending change requests can be approved. Current status: '{changeRequest.Status}'." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        if (!_currentUserService.HasPermission($"{changeRequest.TableName}.approve_change"))
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"You do not have permission to approve changes on '{changeRequest.TableName}'." }],
                StatusCode = HttpStatusCode.Forbidden
            };
        }

        var entity = await GetHeaderEntityTrackedAsync(changeRequest.TableName, changeRequest.RecordId);

        if (entity is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"The {changeRequest.TableName} record no longer exists." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        if (changeRequest.Action == "edit")
        {
            var proposedData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(changeRequest.ProposedData!)!;
            var applyErrors = ApplyProposedData(entity, proposedData);

            if (applyErrors.Count > 0)
            {
                return new ResponsePost
                {
                    Id = id,
                    Messages = applyErrors.Select(e => new Message { Type = MessageType.Error, Description = e }).ToArray(),
                    StatusCode = HttpStatusCode.BadRequest
                };
            }

            UpdateHeaderEntity(changeRequest.TableName, entity);
        }
        else
        {
            RemoveHeaderEntity(changeRequest.TableName, entity);
        }

        changeRequest.Status = "approved";
        changeRequest.ReviewedBy = _currentUserService.AppUserId;
        changeRequest.ReviewedAt = DateTime.UtcNow;
        changeRequest.ReviewNotes = changeRequestReviewDto.ReviewNotes;
        repository.Update(changeRequest);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = changeRequest.ChangeRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = $"Change request approved and applied to {changeRequest.TableName}." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> RejectChangeRequest(long id, ChangeRequestReviewDto changeRequestReviewDto)
    {
        var repository = _unitOfWork.Repository<ChangeRequest>();
        var changeRequest = await repository.GetByIdAsync(id);

        if (changeRequest is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Change request not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (changeRequest.Status != "pending")
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"Only pending change requests can be rejected. Current status: '{changeRequest.Status}'." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        if (!_currentUserService.HasPermission($"{changeRequest.TableName}.approve_change"))
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = $"You do not have permission to review changes on '{changeRequest.TableName}'." }],
                StatusCode = HttpStatusCode.Forbidden
            };
        }

        changeRequest.Status = "rejected";
        changeRequest.ReviewedBy = _currentUserService.AppUserId;
        changeRequest.ReviewedAt = DateTime.UtcNow;
        changeRequest.ReviewNotes = changeRequestReviewDto.ReviewNotes;
        repository.Update(changeRequest);

        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = changeRequest.ChangeRequestId,
            Messages = [new Message { Type = MessageType.Success, Description = "Change request rejected." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private async Task<object?> GetHeaderEntitySnapshotAsync(string tableName, long recordId) => tableName switch
    {
        "receipt" => await _unitOfWork.Repository<Receipt>().Query().FirstOrDefaultAsync(r => r.ReceiptId == recordId),
        "issue" => await _unitOfWork.Repository<Issue>().Query().FirstOrDefaultAsync(i => i.IssueId == recordId),
        "transfer" => await _unitOfWork.Repository<Transfer>().Query().FirstOrDefaultAsync(t => t.TransferId == recordId),
        _ => null
    };

    private async Task<object?> GetHeaderEntityTrackedAsync(string tableName, long recordId) => tableName switch
    {
        "receipt" => await _unitOfWork.Repository<Receipt>().GetByIdAsync(recordId),
        "issue" => await _unitOfWork.Repository<Issue>().GetByIdAsync(recordId),
        "transfer" => await _unitOfWork.Repository<Transfer>().GetByIdAsync(recordId),
        _ => null
    };

    private void UpdateHeaderEntity(string tableName, object entity)
    {
        switch (tableName)
        {
            case "receipt": _unitOfWork.Repository<Receipt>().Update((Receipt)entity); break;
            case "issue": _unitOfWork.Repository<Issue>().Update((Issue)entity); break;
            case "transfer": _unitOfWork.Repository<Transfer>().Update((Transfer)entity); break;
        }
    }

    private void RemoveHeaderEntity(string tableName, object entity)
    {
        switch (tableName)
        {
            case "receipt": _unitOfWork.Repository<Receipt>().Remove((Receipt)entity); break;
            case "issue": _unitOfWork.Repository<Issue>().Remove((Issue)entity); break;
            case "transfer": _unitOfWork.Repository<Transfer>().Remove((Transfer)entity); break;
        }
    }

    /// <summary>
    /// Mutates <paramref name="entity"/> in place by writing each proposed field onto its matching
    /// public property via reflection. The primary key property (e.g. ReceiptId) can never be targeted.
    /// Returns a list of human-readable errors for any field that could not be applied.
    /// </summary>
    private static List<string> ApplyProposedData(object entity, Dictionary<string, JsonElement> proposedData)
    {
        var errors = new List<string>();
        var entityType = entity.GetType();
        var idPropertyName = entityType.Name + "Id";

        foreach (var (key, value) in proposedData)
        {
            var property = entityType.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

            if (property is null || !property.CanWrite)
            {
                errors.Add($"'{key}' is not a valid editable field for '{entityType.Name}'.");
                continue;
            }

            if (string.Equals(property.Name, idPropertyName, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"'{key}' cannot be modified.");
                continue;
            }

            try
            {
                var deserializedValue = value.Deserialize(property.PropertyType);
                property.SetValue(entity, deserializedValue);
            }
            catch (Exception)
            {
                errors.Add($"'{key}' has an invalid value for its type.");
            }
        }

        return errors;
    }

    private static ChangeRequestDto ToDto(ChangeRequest changeRequest) => new()
    {
        ChangeRequestId = changeRequest.ChangeRequestId,
        TableName = changeRequest.TableName,
        RecordId = changeRequest.RecordId,
        Action = changeRequest.Action,
        CurrentData = changeRequest.CurrentData,
        ProposedData = changeRequest.ProposedData,
        Reason = changeRequest.Reason,
        Status = changeRequest.Status,
        RequestedBy = changeRequest.RequestedBy,
        RequestedAt = changeRequest.RequestedAt,
        ReviewedBy = changeRequest.ReviewedBy,
        ReviewedAt = changeRequest.ReviewedAt,
        ReviewNotes = changeRequest.ReviewNotes
    };
}
