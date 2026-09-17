using CheckInvoice.core.Entities.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ChangeRequestConfiguration : IEntityTypeConfiguration<ChangeRequest>
{
    public void Configure(EntityTypeBuilder<ChangeRequest> entity)
    {
        entity.ToTable("change_request");

        entity.HasKey(e => e.ChangeRequestId);
        entity.Property(e => e.ChangeRequestId)
              .HasColumnName("change_request_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.TableName)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("table_name");

        entity.Property(e => e.RecordId)
              .IsRequired()
              .HasColumnName("record_id");

        entity.Property(e => e.Action)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("action");

        entity.Property(e => e.CurrentData)
              .HasColumnName("current_data")
              .HasColumnType("jsonb");

        entity.Property(e => e.ProposedData)
              .HasColumnName("proposed_data")
              .HasColumnType("jsonb");

        entity.Property(e => e.Reason)
              .IsRequired()
              .HasMaxLength(255)
              .HasColumnName("reason");

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
