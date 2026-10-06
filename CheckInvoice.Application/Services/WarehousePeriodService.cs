using System.Globalization;
using System.Net;
using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.Application.Interfaces.Security;
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

public class WarehousePeriodService : IWarehousePeriodService
{
    // La tabla no tiene una restricción única sobre el nombre, así que dos pedidos al mismo tiempo
    // (la pantalla de Entrada/Salida carga los períodos en paralelo) podían crear el mismo mes
    // varias veces. Todo lo que crea o renombra un período pasa por este candado. Es estático
    // porque el servicio es Scoped y el backend corre en una sola instancia.
    private static readonly SemaphoreSlim PeriodWriteLock = new(1, 1);

    private readonly IUnitOfWork _unitOfWork;
    private readonly PaginationOptions _paginationOptions;
    private readonly IValidator<WarehousePeriodDto> _validator;
    private readonly ICurrentUserService _currentUserService;

    public WarehousePeriodService(
        IUnitOfWork unitOfWork,
        PaginationOptions paginationOptions,
        IValidator<WarehousePeriodDto> validator,
        ICurrentUserService currentUserService)
    {
        _unitOfWork = unitOfWork;
        _paginationOptions = paginationOptions;
        _validator = validator;
        _currentUserService = currentUserService;
    }

    public async Task<ResponseGetObject> GetAllWarehousePeriods(PaginationQueryFilter paginationQueryFilter, WarehousePeriodQueryFilter warehousePeriodQueryFilter)
    {
        var pageSize = paginationQueryFilter.PageSize > 0 ? paginationQueryFilter.PageSize : _paginationOptions.InitialPageSize;
        var pageNumber = paginationQueryFilter.PageNumber > 0 ? paginationQueryFilter.PageNumber : _paginationOptions.InitialPageNumber;

        await EnsureCurrentPeriodsExistAsync();

        var query = _unitOfWork.Repository<WarehousePeriod>().Query();

        if (warehousePeriodQueryFilter.WarehousePeriodId.HasValue)
        {
            query = query.Where(w => w.WarehousePeriodId == warehousePeriodQueryFilter.WarehousePeriodId.Value);
        }

        if (!string.IsNullOrWhiteSpace(paginationQueryFilter.SearchCriteria))
        {
            query = query.Where(w => w.Name.Contains(paginationQueryFilter.SearchCriteria));
        }

        var totalRecords = await query.CountAsync();

        var warehousePeriods = await query
            .OrderBy(w => w.Name)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new ResponseGetObject
        {
            Data = new PagedResult<WarehousePeriodDto>
            {
                Items = warehousePeriods.Select(ToDto),
                TotalRecords = totalRecords,
                PageNumber = pageNumber,
                PageSize = pageSize
            },
            Messages = [],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> InsertWarehousePeriod(WarehousePeriodDto warehousePeriodDto)
    {
        var validationResult = await _validator.ValidateAsync(warehousePeriodDto);
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

        var warehousePeriod = new WarehousePeriod
        {
            Name = warehousePeriodDto.Name
        };

        await PeriodWriteLock.WaitAsync();
        try
        {
            if (await NameExistsAsync(warehousePeriodDto.Name, null))
            {
                return DuplicateNameResponse(0, warehousePeriodDto.Name);
            }

            await _unitOfWork.Repository<WarehousePeriod>().AddAsync(warehousePeriod);
            await _unitOfWork.SaveChangesAsync();
        }
        finally
        {
            PeriodWriteLock.Release();
        }

        return new ResponsePost
        {
            Id = warehousePeriod.WarehousePeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "Período de almacén creado correctamente." }],
            StatusCode = HttpStatusCode.Created
        };
    }

    public async Task<ResponsePost> UpdateWarehousePeriod(long id, WarehousePeriodDto warehousePeriodDto)
    {
        var validationResult = await _validator.ValidateAsync(warehousePeriodDto);
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

        var repository = _unitOfWork.Repository<WarehousePeriod>();
        var warehousePeriod = await repository.GetByIdAsync(id);

        if (warehousePeriod is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró el período de almacén." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        await PeriodWriteLock.WaitAsync();
        try
        {
            if (await NameExistsAsync(warehousePeriodDto.Name, id))
            {
                return DuplicateNameResponse(id, warehousePeriodDto.Name);
            }

            warehousePeriod.Name = warehousePeriodDto.Name;

            repository.Update(warehousePeriod);
            await _unitOfWork.SaveChangesAsync();
        }
        finally
        {
            PeriodWriteLock.Release();
        }

        return new ResponsePost
        {
            Id = warehousePeriod.WarehousePeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "Período de almacén actualizado correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> DeleteWarehousePeriod(long id)
    {
        var repository = _unitOfWork.Repository<WarehousePeriod>();
        var warehousePeriod = await repository.GetByIdAsync(id);

        if (warehousePeriod is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró el período de almacén." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        repository.Remove(warehousePeriod);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehousePeriod.WarehousePeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "Período de almacén eliminado correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    public async Task<ResponsePost> CloseWarehousePeriod(long id)
    {
        var repository = _unitOfWork.Repository<WarehousePeriod>();
        var warehousePeriod = await repository.GetByIdAsync(id);

        if (warehousePeriod is null)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "No se encontró el período de almacén." }],
                StatusCode = HttpStatusCode.NotFound
            };
        }

        if (warehousePeriod.IsClosed)
        {
            return new ResponsePost
            {
                Id = id,
                Messages = [new Message { Type = MessageType.Error, Description = "Este período de almacén ya está cerrado." }],
                StatusCode = HttpStatusCode.BadRequest
            };
        }

        warehousePeriod.IsClosed = true;
        warehousePeriod.ClosedAt = DateTime.UtcNow;
        warehousePeriod.ClosedById = _currentUserService.AppUserId;

        repository.Update(warehousePeriod);
        await _unitOfWork.SaveChangesAsync();

        return new ResponsePost
        {
            Id = warehousePeriod.WarehousePeriodId,
            Messages = [new Message { Type = MessageType.Success, Description = "Período de almacén cerrado correctamente." }],
            StatusCode = HttpStatusCode.OK
        };
    }

    // Entradas y Salidas solo dejan elegir el período del mes actual o el anterior. Si nadie
    // creaba a mano el período del mes nuevo, el 1ro del mes el select quedaba solo con el
    // anterior. Se crean aquí los que falten, antes de listar, para que siempre estén los dos.
    // El mes se toma en hora de Bolivia, igual que el select del frontend.
    private async Task EnsureCurrentPeriodsExistAsync()
    {
        var now = BoliviaTime.Now;
        var requiredNames = new[]
        {
            new DateTime(now.Year, now.Month, 1).AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture),
            now.ToString("yyyy-MM", CultureInfo.InvariantCulture)
        };

        var repository = _unitOfWork.Repository<WarehousePeriod>();

        await PeriodWriteLock.WaitAsync();
        try
        {
            // Se consulta recién con el candado tomado: si otro pedido acababa de crear el mes,
            // aquí ya se ve y no se repite.
            var existingNames = await repository.Query()
                .Where(w => requiredNames.Contains(w.Name))
                .Select(w => w.Name)
                .ToListAsync();

            var missingNames = requiredNames.Except(existingNames).ToList();
            if (missingNames.Count == 0)
            {
                return;
            }

            foreach (var name in missingNames)
            {
                await repository.AddAsync(new WarehousePeriod { Name = name });
            }

            await _unitOfWork.SaveChangesAsync();
        }
        finally
        {
            PeriodWriteLock.Release();
        }
    }

    private Task<bool> NameExistsAsync(string name, long? excludeId) =>
        _unitOfWork.Repository<WarehousePeriod>().Query()
            .AnyAsync(w => w.Name == name && (excludeId == null || w.WarehousePeriodId != excludeId));

    private static ResponsePost DuplicateNameResponse(long id, string name) => new()
    {
        Id = id,
        Messages = [new Message { Type = MessageType.Error, Description = $"Ya existe un período de almacén con el nombre \"{name}\"." }],
        StatusCode = HttpStatusCode.BadRequest
    };

    private static WarehousePeriodDto ToDto(WarehousePeriod warehousePeriod) => new()
    {
        WarehousePeriodId = warehousePeriod.WarehousePeriodId,
        Name = warehousePeriod.Name,
        IsClosed = warehousePeriod.IsClosed,
        ClosedAt = warehousePeriod.ClosedAt,
        ClosedById = warehousePeriod.ClosedById
    };
}