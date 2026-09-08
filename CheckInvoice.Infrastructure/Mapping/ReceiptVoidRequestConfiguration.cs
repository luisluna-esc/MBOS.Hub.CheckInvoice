using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ReceiptVoidRequestConfiguration : IEntityTypeConfiguration<ReceiptVoidRequest>
{
    public void Configure(EntityTypeBuilder<ReceiptVoidRequest> entity)
    {
        entity.ToTable("receipt_void_request");

        entity.HasKey(e => e.ReceiptVoidRequestId);
        entity.Property(e => e.ReceiptVoidRequestId)
              .HasColumnName("receipt_void_request_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.ReceiptId)
              .IsRequired()
              .HasColumnName("receipt_id");

        entity.Property(e => e.VoidReasonId)
              .IsRequired()
              .HasColumnName("void_reason_id");

        entity.Property(e => e.Detail)
              .HasMaxLength(255)
              .HasColumnName("detail");

        entity.Property(e => e.Status)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("status");

        entity.Property(e => e.RequestedBy)
              .IsRequired()
              .HasColumnName("requested_by");

        entity.Property(e => e.RequestedAt)
              .IsRequired()
              .HasColumnName("requested_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.ReviewedBy)
              .HasColumnName("reviewed_by");

        entity.Property(e => e.ReviewedAt)
              .HasColumnName("reviewed_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.ReviewNotes)
              .HasMaxLength(255)
              .HasColumnName("review_notes");
    }
}
