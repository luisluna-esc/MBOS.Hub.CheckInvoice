using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class MissionConfiguration : IEntityTypeConfiguration<Mission>
{
    public void Configure(EntityTypeBuilder<Mission> entity)
    {
        entity.ToTable("mission");

        entity.HasKey(e => e.MissionId);
        entity.Property(e => e.MissionId)
              .HasColumnName("mission_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");
    }
}