using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ReceiptDetailConfiguration : IEntityTypeConfiguration<ReceiptDetail>
{
    public void Configure(EntityTypeBuilder<ReceiptDetail> entity)
    {
        entity.ToTable("receipt_detail");

        entity.HasKey(e => e.ReceiptDetailId);
        entity.Property(e => e.ReceiptDetailId)
              .HasColumnName("receipt_detail_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.ReceiptId)
              .IsRequired()
              .HasColumnName("receipt_id");

        entity.Property(e => e.ProductId)
              .IsRequired()
              .HasColumnName("product_id");

        entity.Property(e => e.Quantity)
              .IsRequired()
              .HasColumnName("quantity")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.UnitCost)
              .IsRequired()
              .HasColumnName("unit_cost")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.TotalCost)
              .IsRequired()
              .HasColumnName("total_cost")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.WorkOrder)
              .HasMaxLength(50)
              .HasColumnName("work_order");

        entity.Property(e => e.Detail)
              .HasMaxLength(255)
              .HasColumnName("detail");
    }
}