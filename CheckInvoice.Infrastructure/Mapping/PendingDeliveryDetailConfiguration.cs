using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PendingDeliveryDetailConfiguration : IEntityTypeConfiguration<PendingDeliveryDetail>
{
    public void Configure(EntityTypeBuilder<PendingDeliveryDetail> entity)
    {
        entity.ToTable("pending_delivery_detail");

        entity.HasKey(e => e.PendingDeliveryDetailId);
        entity.Property(e => e.PendingDeliveryDetailId)
              .HasColumnName("pending_delivery_detail_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.PendingDeliveryId)
              .IsRequired()
              .HasColumnName("pending_delivery_id");

        entity.Property(e => e.ProductId)
              .IsRequired()
              .HasColumnName("product_id");

        entity.Property(e => e.Quantity)
              .IsRequired()
              .HasColumnType("numeric(12,2)")
              .HasColumnName("quantity");
    }
}
