using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class IssueConfiguration : IEntityTypeConfiguration<Issue>
{
    public void Configure(EntityTypeBuilder<Issue> entity)
    {
        entity.ToTable("issue");

        entity.HasKey(e => e.IssueId);
        entity.Property(e => e.IssueId)
              .HasColumnName("issue_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.IssueTypeId)
              .HasColumnName("issue_type_id");

        entity.Property(e => e.WarehouseId)
              .HasColumnName("warehouse_id");

        entity.Property(e => e.WarehousePeriodId)
              .HasColumnName("warehouse_period_id");

        entity.Property(e => e.ClientId)
              .HasColumnName("client_id");

        entity.Property(e => e.Complement)
              .HasMaxLength(10)
              .HasColumnName("complement");

        entity.Property(e => e.IssueDate)
              .IsRequired()
              .HasColumnName("issue_date")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.PrintTypeId)
              .HasColumnName("print_type_id");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");
    }
}