using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class InventoryCountConfiguration : IEntityTypeConfiguration<InventoryCount>
{
    public void Configure(EntityTypeBuilder<InventoryCount> entity)
    {
        entity.ToTable("inventory_count");

        entity.HasKey(e => e.InventoryCountId);
        entity.Property(e => e.InventoryCountId)
              .HasColumnName("inventory_count_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.WarehouseId)
              .IsRequired()
              .HasColumnName("warehouse_id");

        entity.Property(e => e.SourceCountId)
              .HasColumnName("source_count_id");

        entity.Property(e => e.StartDate)
              .IsRequired()
              .HasColumnName("start_date")
              .HasColumnType("date");

        entity.Property(e => e.EndDate)
              .IsRequired()
              .HasColumnName("end_date")
              .HasColumnType("date");

        entity.Property(e => e.CountDate)
              .IsRequired()
              .HasColumnName("count_date")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.Status)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("status");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");

        entity.Property(e => e.CreatedAt)
              .IsRequired()
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.ClosedById)
              .HasColumnName("closed_by");

        entity.Property(e => e.ClosedAt)
              .HasColumnName("closed_at")
              .HasColumnType("timestamp without time zone");
    }
}