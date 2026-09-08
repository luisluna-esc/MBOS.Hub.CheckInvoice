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

public class ShipmentService : IShipmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<ShipmentDto> _validator;

    public ShipmentService(IUnitOfWork unitOfWork, PaginationOptions paginationOptions, IValidator<ShipmentDto> validator)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
    }

    public async Task<ResponseGetObject> GetAllShipments(PaginationQueryFilter paginationQueryFilter, ShipmentQueryFilter shipmentQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        var query = _unitOfWork.Repository<Shipment>().Query();

        if (shipmentQueryFilter.ShipmentId.HasValue)
        {
            query = query.Where(s => s.ShipmentId == shipmentQueryFilter.ShipmentId.Value);
        }

        if (shipmentQueryFilter.ShipmentNumber.HasValue)
        {
            query = query.Where(s => s.ShipmentNumber == shipmentQueryFilter.ShipmentNumber.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(s => s.Description != null && s.Description.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var shipments = await query
            .OrderByDescending(s => s.ShipmentDate)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<ShipmentDto>
            {
                Items = shipments.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertShipment(ShipmentDto shipmentDto)
    {
        var validationResult = await _validator.ValidateAsync(shipmentDto);
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

        var shipment = new Shipment
        {
            ShipmentNumber = shipmentDto.ShipmentNumber,
            ShipmentDate = shipmentDto.ShipmentDate,
            Description = shipmentDto.Description
        };

        await _unitOfWork.Repository<Shipment>().AddAsync(shipment);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = shipment.ShipmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Shipment created successfully." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateShipment(long id, ShipmentDto shipmentDto)
    {
        var validationResult = await _validator.ValidateAsync(shipmentDto);
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

        var repository = _unitOfWork.Repository<Shipment>();
        var shipment = await repository.GetByIdAsync(id);

        if (shipment is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Shipment not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        shipment.ShipmentNumber = shipmentDto.ShipmentNumber;
        shipment.ShipmentDate = shipmentDto.ShipmentDate;
        shipment.Description = shipmentDto.Description;

        repository.Update(shipment);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = shipment.ShipmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Shipment updated successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteShipment(long id)
    {
        var repository = _unitOfWork.Repository<Shipment>();
        var shipment = await repository.GetByIdAsync(id);

        if (shipment is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Shipment not found." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(shipment);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = shipment.ShipmentId,
            Messages = [new Message { Type = MessageType.Success, Description = "Shipment deleted successfully." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    private static ShipmentDto ToDto(Shipment shipment) => new()
    {
        ShipmentId = shipment.ShipmentId,
        ShipmentNumber = shipment.ShipmentNumber,
        ShipmentDate = shipment.ShipmentDate,
        Description = shipment.Description
    };
}