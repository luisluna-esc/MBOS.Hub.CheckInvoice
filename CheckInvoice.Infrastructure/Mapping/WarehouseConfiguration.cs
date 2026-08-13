using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
{
    public void Configure(EntityTypeBuilder<Warehouse> entity)
    {
        entity.ToTable("warehouse");

        entity.HasKey(e => e.WarehouseId);
        entity.Property(e => e.WarehouseId)
              .HasColumnName("warehouse_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.Property(e => e.Address)
              .HasMaxLength(255)
              .HasColumnName("address");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");
    }
}