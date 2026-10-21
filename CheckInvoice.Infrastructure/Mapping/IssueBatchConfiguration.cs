using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class IssueBatchConfiguration : IEntityTypeConfiguration<IssueBatch>
{
    public void Configure(EntityTypeBuilder<IssueBatch> entity)
    {
        entity.ToTable("issue_batch");

        entity.HasKey(e => e.IssueBatchId);
        entity.Property(e => e.IssueBatchId)
              .HasColumnName("issue_batch_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.MonthlyNumber)
              .IsRequired()
              .HasColumnName("monthly_number");

        entity.Property(e => e.BatchDate)
              .IsRequired()
              .HasColumnName("batch_date")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.WarehouseId)
              .IsRequired()
              .HasColumnName("warehouse_id");

        entity.Property(e => e.IssueTypeId)
              .HasColumnName("issue_type_id");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");

        entity.Property(e => e.CreatedAt)
              .IsRequired()
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");
    }
}
