using CheckInvoice.core.Entities.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> entity)
    {
        entity.ToTable("audit_log");

        entity.HasKey(e => e.AuditLogId);
        entity.Property(e => e.AuditLogId)
              .HasColumnName("audit_log_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.TableName)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("table_name");

        entity.Property(e => e.RecordId)
              .HasColumnName("record_id");

        entity.Property(e => e.LogDate)
              .IsRequired()
              .HasColumnName("log_date")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.Action)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("action");
    }
}