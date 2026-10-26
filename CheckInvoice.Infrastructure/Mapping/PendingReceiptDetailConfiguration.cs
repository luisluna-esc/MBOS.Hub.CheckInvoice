using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PendingReceiptDetailConfiguration : IEntityTypeConfiguration<PendingReceiptDetail>
{
    public void Configure(EntityTypeBuilder<PendingReceiptDetail> entity)
    {
        entity.ToTable("pending_receipt_detail");

        entity.HasKey(e => e.PendingReceiptDetailId);
        entity.Property(e => e.PendingReceiptDetailId)
              .HasColumnName("pending_receipt_detail_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.PendingReceiptId)
              .IsRequired()
              .HasColumnName("pending_receipt_id");

        entity.Property(e => e.ProductId)
              .IsRequired()
              .HasColumnName("product_id");

        entity.Property(e => e.Quantity)
              .IsRequired()
              .HasColumnType("numeric(12,2)")
              .HasColumnName("quantity");
    }
}
