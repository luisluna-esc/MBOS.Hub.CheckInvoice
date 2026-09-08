using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> entity)
    {
        entity.ToTable("shipment");

        entity.HasKey(e => e.ShipmentId);
        entity.Property(e => e.ShipmentId)
              .HasColumnName("shipment_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.ShipmentNumber)
              .IsRequired()
              .HasColumnName("shipment_number");

        entity.Property(e => e.ShipmentDate)
              .IsRequired()
              .HasColumnName("shipment_date")
              .HasColumnType("date");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");
    }
}