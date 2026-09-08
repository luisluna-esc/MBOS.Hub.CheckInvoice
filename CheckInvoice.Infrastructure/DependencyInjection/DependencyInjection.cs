using CheckInvoice.Application.Dtos.Catalogs;
using CheckInvoice.Application.Dtos.Finance;
using CheckInvoice.Application.Dtos.Governance;
using CheckInvoice.Application.Dtos.Movements;
using CheckInvoice.Application.Dtos.Organization;
using CheckInvoice.Application.Dtos.Parties;
using CheckInvoice.Application.Dtos.Products;
using CheckInvoice.Application.Dtos.Security;
using CheckInvoice.Application.Interfaces.Audit;
using CheckInvoice.Application.Interfaces.Catalogs;
using CheckInvoice.Application.Interfaces.Finance;
using CheckInvoice.Application.Interfaces.Governance;
using CheckInvoice.Application.Interfaces.Movements;
using CheckInvoice.Application.Interfaces.Organization;
using CheckInvoice.Application.Interfaces.Parties;
using CheckInvoice.Application.Interfaces.Portal;
using CheckInvoice.Application.Interfaces.Products;
using CheckInvoice.Application.Interfaces.Reports;
using CheckInvoice.Application.Interfaces.Security;
using CheckInvoice.Application.Interfaces.Warehouses;
using CheckInvoice.Application.Services;
using CheckInvoice.Application.Validators.Catalogs;
using CheckInvoice.Application.Validators.Finance;
using CheckInvoice.Application.Validators.Governance;
using CheckInvoice.Application.Validators.Movements;
using CheckInvoice.Application.Validators.Organization;
using CheckInvoice.Application.Validators.Parties;
using CheckInvoice.Application.Validators.Products;
using CheckInvoice.Application.Validators.Security;
using CheckInvoice.core.Configuration;
using CheckInvoice.core.Interfaces;
using CheckInvoice.Infrastructure.Authorization;
using CheckInvoice.Infrastructure.Context;
using CheckInvoice.Infrastructure.Context.Core;
using CheckInvoice.Infrastructure.Interceptors;
using CheckInvoice.Infrastructure.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CheckInvoice.Infrastructure.DependencyInjection;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Missing connection string 'DefaultConnection' in appsettings.");

        services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(connectionString)
                   .AddInterceptors(serviceProvider.GetRequiredService<AuditSaveChangesInterceptor>()));

        services.Configure<PaginationOptions>(configuration.GetSection("Pagination"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<PaginationOptions>>().Value);

        services.Configure<JwtOptions>(configuration.GetSection("Jwt"));
        services.AddScoped(sp => sp.GetRequiredService<IOptions<JwtOptions>>().Value);

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IMissionService, MissionService>();
        services.AddScoped<IValidator<MissionDto>, MissionValidator>();

        services.AddScoped<IRoleService, RoleService>();
        services.AddScoped<IValidator<RoleDto>, RoleValidator>();

        services.AddScoped<IPermissionService, PermissionService>();
        services.AddScoped<IValidator<PermissionDto>, PermissionValidator>();

        services.AddScoped<IRolePermissionService, RolePermissionService>();

        services.AddScoped<IAppUserService, AppUserService>();
        services.AddScoped<IValidator<AppUserDto>, AppUserValidator>();

        services.AddScoped<IAppUserRoleService, AppUserRoleService>();

        services.AddScoped<IMenuService, MenuService>();
        services.AddScoped<IValidator<MenuDto>, MenuValidator>();
        services.AddScoped<IRoleMenuService, RoleMenuService>();

        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();

        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IValidator<ConfigurationDto>, ConfigurationValidator>();

        services.AddScoped<ICountryService, CountryService>();
        services.AddScoped<IValidator<CountryDto>, CountryValidator>();

        services.AddScoped<IDocumentTypeService, DocumentTypeService>();
        services.AddScoped<IValidator<DocumentTypeDto>, DocumentTypeValidator>();

        services.AddScoped<ISpecialCaseService, SpecialCaseService>();
        services.AddScoped<IValidator<SpecialCaseDto>, SpecialCaseValidator>();

        services.AddScoped<IVoidReasonService, VoidReasonService>();

        services.AddScoped<IChurchTypeService, ChurchTypeService>();
        services.AddScoped<IValidator<ChurchTypeDto>, ChurchTypeValidator>();

        services.AddScoped<IIssueTypeService, IssueTypeService>();
        services.AddScoped<IValidator<IssueTypeDto>, IssueTypeValidator>();

        services.AddScoped<IReceiptTypeService, ReceiptTypeService>();
        services.AddScoped<IValidator<ReceiptTypeDto>, ReceiptTypeValidator>();

        services.AddScoped<IPrintTypeService, PrintTypeService>();
        services.AddScoped<IValidator<PrintTypeDto>, PrintTypeValidator>();

        services.AddScoped<IMediaTypeService, MediaTypeService>();
        services.AddScoped<IValidator<MediaTypeDto>, MediaTypeValidator>();

        services.AddScoped<IDepartmentService, DepartmentService>();
        services.AddScoped<IValidator<DepartmentDto>, DepartmentValidator>();

        services.AddScoped<ISubDepartmentService, SubDepartmentService>();
        services.AddScoped<IValidator<SubDepartmentDto>, SubDepartmentValidator>();

        services.AddScoped<IProvinceService, ProvinceService>();
        services.AddScoped<IValidator<ProvinceDto>, ProvinceValidator>();

        services.AddScoped<IWarehousePeriodService, WarehousePeriodService>();
        services.AddScoped<IValidator<WarehousePeriodDto>, WarehousePeriodValidator>();

        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IValidator<WarehouseDto>, WarehouseValidator>();

        services.AddScoped<IShipmentService, ShipmentService>();
        services.AddScoped<IValidator<ShipmentDto>, ShipmentValidator>();

        services.AddScoped<IDistrictService, DistrictService>();
        services.AddScoped<IValidator<DistrictDto>, DistrictValidator>();

        services.AddScoped<IChurchService, ChurchService>();
        services.AddScoped<IValidator<ChurchDto>, ChurchValidator>();

        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IValidator<ClientDto>, ClientValidator>();

        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IValidator<SupplierDto>, SupplierValidator>();

        services.AddScoped<IClientDistrictHistoryService, ClientDistrictHistoryService>();
        services.AddScoped<IValidator<ClientDistrictHistoryDto>, ClientDistrictHistoryValidator>();

        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IValidator<ProductDto>, ProductValidator>();

        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IStockService, StockService>();

        services.AddScoped<IReceiptService, ReceiptService>();
        services.AddScoped<IValidator<ReceiptRequestDto>, ReceiptRequestValidator>();

        services.AddScoped<IIssueService, IssueService>();
        services.AddScoped<IValidator<IssueRequestDto>, IssueRequestValidator>();

        services.AddScoped<IIssueVoidRequestService, IssueVoidRequestService>();
        services.AddScoped<IValidator<IssueVoidRequestCreateDto>, IssueVoidRequestCreateValidator>();

        services.AddScoped<IReceiptVoidRequestService, ReceiptVoidRequestService>();
        services.AddScoped<IValidator<ReceiptVoidRequestCreateDto>, ReceiptVoidRequestCreateValidator>();

        services.AddScoped<ITransferService, TransferService>();
        services.AddScoped<IValidator<TransferRequestDto>, TransferRequestValidator>();

        services.AddScoped<IInventoryCountService, InventoryCountService>();
        services.AddScoped<IValidator<InventoryCountRequestDto>, InventoryCountRequestValidator>();

        services.AddScoped<IDiscountService, DiscountService>();
        services.AddScoped<IValidator<DiscountDto>, DiscountValidator>();

        services.AddScoped<IAccountReceivableService, AccountReceivableService>();
        services.AddScoped<IValidator<AccountReceivableDto>, AccountReceivableValidator>();

        services.AddScoped<IReportService, ReportService>();

        services.AddScoped<IInstallmentService, InstallmentService>();
        services.AddScoped<IValidator<InstallmentDto>, InstallmentValidator>();

        services.AddScoped<IPaymentService, PaymentService>();
        services.AddScoped<IValidator<PaymentDto>, PaymentValidator>();

        services.AddScoped<IPortalService, PortalService>();

        services.AddScoped<IChangeRequestService, ChangeRequestService>();
        services.AddScoped<IValidator<ChangeRequestCreateDto>, ChangeRequestCreateValidator>();

        return services;
    }
}