using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class InventoryCountDetailConfiguration : IEntityTypeConfiguration<InventoryCountDetail>
{
    public void Configure(EntityTypeBuilder<InventoryCountDetail> entity)
    {
        entity.ToTable("inventory_count_detail");

        entity.HasKey(e => e.InventoryCountDetailId);
        entity.Property(e => e.InventoryCountDetailId)
              .HasColumnName("inventory_count_detail_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.InventoryCountId)
              .IsRequired()
              .HasColumnName("inventory_count_id");

        entity.Property(e => e.ProductId)
              .IsRequired()
              .HasColumnName("product_id");

        entity.Property(e => e.SystemQuantity)
              .IsRequired()
              .HasColumnName("system_quantity")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.UnitValue)
              .HasColumnName("unit_value")
              .HasColumnType("numeric(12,4)");

        entity.Property(e => e.PhysicalQuantity)
              .HasColumnName("physical_quantity")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.Difference)
              .HasColumnName("difference")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.Notes)
              .HasMaxLength(255)
              .HasColumnName("notes");
    }
}