using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class TransferVoidRequestConfiguration : IEntityTypeConfiguration<TransferVoidRequest>
{
    public void Configure(EntityTypeBuilder<TransferVoidRequest> entity)
    {
        entity.ToTable("transfer_void_request");

        entity.HasKey(e => e.TransferVoidRequestId);
        entity.Property(e => e.TransferVoidRequestId)
              .HasColumnName("transfer_void_request_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.TransferId)
              .IsRequired()
              .HasColumnName("transfer_id");

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
