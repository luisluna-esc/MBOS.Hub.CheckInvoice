using CheckInvoice.core.Entities.Governance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class DocumentReviewConfiguration : IEntityTypeConfiguration<DocumentReview>
{
    public void Configure(EntityTypeBuilder<DocumentReview> entity)
    {
        entity.ToTable("document_review");

        entity.HasKey(e => e.DocumentReviewId);
        entity.Property(e => e.DocumentReviewId)
              .HasColumnName("document_review_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.DocumentType)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("document_type");

        entity.Property(e => e.DocumentId)
              .IsRequired()
              .HasColumnName("document_id");

        entity.Property(e => e.Version)
              .IsRequired()
              .HasColumnName("version");

        entity.Property(e => e.Action)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("action");

        entity.Property(e => e.UserId)
              .HasColumnName("user_id");

        entity.Property(e => e.At)
              .IsRequired()
              .HasColumnName("at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.Reason)
              .HasMaxLength(255)
              .HasColumnName("reason");

        entity.Property(e => e.ContentHash)
              .HasMaxLength(64)
              .HasColumnName("content_hash");
    }
}
