using CheckInvoice.core.Entities.Audit;
using CheckInvoice.core.Entities.Catalogs;
using CheckInvoice.core.Entities.Finance;
using CheckInvoice.core.Entities.Governance;
using CheckInvoice.core.Entities.Movements;
using CheckInvoice.core.Entities.Organization;
using CheckInvoice.core.Entities.Parties;
using CheckInvoice.core.Entities.Products;
using CheckInvoice.core.Entities.Security;
using CheckInvoice.core.Entities.Warehouses;
using CheckInvoice.Infrastructure.Mapping;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CheckInvoice.Infrastructure.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Mission> Missions => Set<Mission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<AppUserRole> AppUserRoles => Set<AppUserRole>();
    public DbSet<Menu> Menus => Set<Menu>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Configuration> Configurations => Set<Configuration>();
    public DbSet<Country> Countries => Set<Country>();
    public DbSet<DocumentType> DocumentTypes => Set<DocumentType>();
    public DbSet<ChurchType> ChurchTypes => Set<ChurchType>();
    public DbSet<IssueType> IssueTypes => Set<IssueType>();
    public DbSet<ReceiptType> ReceiptTypes => Set<ReceiptType>();
    public DbSet<PrintType> PrintTypes => Set<PrintType>();
    public DbSet<MediaType> MediaTypes => Set<MediaType>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<SubDepartment> SubDepartments => Set<SubDepartment>();
    public DbSet<Province> Provinces => Set<Province>();
    public DbSet<WarehousePeriod> WarehousePeriods => Set<WarehousePeriod>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<Church> Churches => Set<Church>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<ClientDistrictHistory> ClientDistrictHistories => Set<ClientDistrictHistory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Stock> Stocks => Set<Stock>();
    public DbSet<Receipt> Receipts => Set<Receipt>();
    public DbSet<ReceiptDetail> ReceiptDetails => Set<ReceiptDetail>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueDetail> IssueDetails => Set<IssueDetail>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<TransferDetail> TransferDetails => Set<TransferDetail>();
    public DbSet<InventoryCount> InventoryCounts => Set<InventoryCount>();
    public DbSet<InventoryCountDetail> InventoryCountDetails => Set<InventoryCountDetail>();
    public DbSet<Discount> Discounts => Set<Discount>();
    public DbSet<AccountReceivable> AccountReceivables => Set<AccountReceivable>();
    public DbSet<Installment> Installments => Set<Installment>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<ChangeRequest> ChangeRequests => Set<ChangeRequest>();
    public DbSet<VoidReason> VoidReasons => Set<VoidReason>();
    public DbSet<IssueVoidRequest> IssueVoidRequests => Set<IssueVoidRequest>();
    public DbSet<ReceiptVoidRequest> ReceiptVoidRequests => Set<ReceiptVoidRequest>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MissionConfiguration).Assembly);

        // All "timestamp" columns in the schema are without time zone, but the app always works in UTC.
        // This converter strips the Kind on write and restores it to Utc on read, so every DateTime
        // property across every entity behaves consistently without repeating this per property.
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Unspecified) : v,
            v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTime))
                    property.SetValueConverter(utcConverter);
                else if (property.ClrType == typeof(DateTime?))
                    property.SetValueConverter(nullableUtcConverter);
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}