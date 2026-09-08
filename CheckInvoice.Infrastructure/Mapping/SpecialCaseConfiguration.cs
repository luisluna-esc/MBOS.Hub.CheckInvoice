using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class SpecialCaseConfiguration : IEntityTypeConfiguration<SpecialCase>
{
    public void Configure(EntityTypeBuilder<SpecialCase> entity)
    {
        entity.ToTable("special_case");

        entity.HasKey(e => e.SpecialCaseId);
        entity.Property(e => e.SpecialCaseId)
              .HasColumnName("special_case_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Code)
              .IsRequired()
              .HasMaxLength(10)
              .HasColumnName("code");

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.HasIndex(e => e.Code).IsUnique();
    }
}
