using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class WarehousePeriodConfiguration : IEntityTypeConfiguration<WarehousePeriod>
{
    public void Configure(EntityTypeBuilder<WarehousePeriod> entity)
    {
        entity.ToTable("warehouse_period");

        entity.HasKey(e => e.WarehousePeriodId);
        entity.Property(e => e.WarehousePeriodId)
              .HasColumnName("warehouse_period_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("name");

        entity.Property(e => e.IsClosed)
              .IsRequired()
              .HasColumnName("is_closed");

        entity.Property(e => e.ClosedAt)
              .HasColumnName("closed_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.ClosedById)
              .HasColumnName("closed_by");
    }
}