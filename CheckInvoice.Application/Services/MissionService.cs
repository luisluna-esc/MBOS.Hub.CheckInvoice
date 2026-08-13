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

public class MissionService : IMissionService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<MissionDto> _validator;

    public MissionService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<MissionDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllMissions(PaginationQueryFilter paginationQueryFilter, MissionQueryFilter missionQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Mission>().Query();

        if (missionQueryFilter.MissionId.HasValue)
        {
            query = query.Where(m => m.MissionId == missionQueryFilter.MissionId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(m => m.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        if (missionQueryFilter.IsActive.HasValue)
        {
            query = query.Where(m => m.IsActive == missionQueryFilter.IsActive.Value);
        }

        var totalRecords = await query.CountAsync();

        var missions = await query
            .OrderBy(m => m.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<MissionDto>
            {
                Items = missions.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertMission(MissionDto missionDto)
    {
        var validationResult = await _validator.ValidateAsync(missionDto);
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

        var mission = new Mission
        {
            Name = missionDto.Name,
            IsActive = missionDto.IsActive
        };

        await _unitOfWork.Repository<Mission>().AddAsync(mission);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = mission.MissionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Mission created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateMission(long id, MissionDto missionDto)
    {
        var validationResult = await _validator.ValidateAsync(missionDto);
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

        var repository = _unitOfWork.Repository<Mission>();
        var mission = await repository.GetByIdAsync(id);

        if (mission is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Mission not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        mission.Name = missionDto.Name;
        mission.IsActive = missionDto.IsActive;

        repository.Update(mission);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = mission.MissionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Mission updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteMission(long id)
    {
        var repository = _unitOfWork.Repository<Mission>();
        var mission = await repository.GetByIdAsync(id);
        
        if (mission is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Mission not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(mission);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = mission.MissionId,
            Messages = [new Message { Type = MessageType.Success, Description = "Mission deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static MissionDto ToDto(Mission mission) => new()
    {
        MissionId = mission.MissionId,
        Name = mission.Name,
        IsActive = mission.IsActive
    };
}