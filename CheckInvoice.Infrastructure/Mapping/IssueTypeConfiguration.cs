using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class IssueTypeConfiguration : IEntityTypeConfiguration<IssueType>
{
    public void Configure(EntityTypeBuilder<IssueType> entity)
    {
        entity.ToTable("issue_type");

        entity.HasKey(e => e.IssueTypeId);
        entity.Property(e => e.IssueTypeId)
              .HasColumnName("issue_type_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("name");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");
    }
}