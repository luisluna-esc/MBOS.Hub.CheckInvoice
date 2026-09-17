using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.core.Entities.ResponseApi.DisplayFormat;
using CheckInvoice.core.QueryFilters.Catalogs;
using CheckInvoice.core.QueryFilters.Pagination;

namespace CheckInvoice.Application.Interfaces.Catalogs;

public interface IShipmentService
{
    Task<ResponseGetObject> GetAllShipments(PaginationQueryFilter paginationQueryFilter, ShipmentQueryFilter shipmentQueryFilter);
    Task<ResponsePost> InsertShipment(ShipmentDto shipmentDto);
    Task<ResponsePost> UpdateShipment(long id, ShipmentDto shipmentDto);
    Task<ResponsePost> DeleteShipment(long id);
}