using CheckInvoice.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ChurchConfiguration : IEntityTypeConfiguration<Church>
{
    public void Configure(EntityTypeBuilder<Church> entity)
    {
        entity.ToTable("church");

        entity.HasKey(e => e.ChurchId);
        entity.Property(e => e.ChurchId)
              .HasColumnName("church_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Code)
              .HasMaxLength(20)
              .HasColumnName("code");

        entity.Property(e => e.ChurchName)
              .IsRequired()
              .HasMaxLength(150)
              .HasColumnName("church_name");

        entity.Property(e => e.DistrictId)
              .HasColumnName("district_id");

        entity.Property(e => e.ChurchTypeId)
              .HasColumnName("church_type_id");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");
    }
}