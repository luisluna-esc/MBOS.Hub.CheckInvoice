using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PendingReceiptConfiguration : IEntityTypeConfiguration<PendingReceipt>
{
    public void Configure(EntityTypeBuilder<PendingReceipt> entity)
    {
        entity.ToTable("pending_receipt");

        entity.HasKey(e => e.PendingReceiptId);
        entity.Property(e => e.PendingReceiptId)
              .HasColumnName("pending_receipt_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.MonthlyNumber)
              .IsRequired()
              .HasColumnName("monthly_number");

        entity.Property(e => e.WarehouseId)
              .IsRequired()
              .HasColumnName("warehouse_id");

        entity.Property(e => e.SupplierId)
              .HasColumnName("supplier_id");

        entity.Property(e => e.ReceivedDate)
              .IsRequired()
              .HasColumnType("timestamp without time zone")
              .HasColumnName("received_date");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");

        entity.Property(e => e.Status)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("status");

        entity.Property(e => e.ReceiptId)
              .HasColumnName("receipt_id");

        entity.Property(e => e.CreatedAt)
              .IsRequired()
              .HasColumnType("timestamp without time zone")
              .HasColumnName("created_at");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");

        entity.Property(e => e.ClosedAt)
              .HasColumnType("timestamp without time zone")
              .HasColumnName("closed_at");

        entity.Property(e => e.ClosedById)
              .HasColumnName("closed_by");
    }
}
