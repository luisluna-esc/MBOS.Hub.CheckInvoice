using CheckInvoice.core.Entities.Organization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class DistrictConfiguration : IEntityTypeConfiguration<District>
{
    public void Configure(EntityTypeBuilder<District> entity)
    {
        entity.ToTable("district");

        entity.HasKey(e => e.DistrictId);
        entity.Property(e => e.DistrictId)
              .HasColumnName("district_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Code)
              .HasMaxLength(20)
              .HasColumnName("code");

        entity.Property(e => e.DistrictName)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("district_name");

        entity.Property(e => e.MissionId)
              .HasColumnName("mission_id");

        entity.Property(e => e.ProvinceId)
              .HasColumnName("province_id");

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.RegistrationDate)
              .IsRequired()
              .HasColumnName("registration_date")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");
    }
}