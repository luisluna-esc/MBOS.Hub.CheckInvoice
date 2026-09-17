using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ConfigurationConfiguration : IEntityTypeConfiguration<Configuration>
{
    public void Configure(EntityTypeBuilder<Configuration> entity)
    {
        entity.ToTable("configuration");

        entity.HasKey(e => e.ConfigurationId);
        entity.Property(e => e.ConfigurationId)
              .HasColumnName("configuration_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Key)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("key");

        entity.Property(e => e.Value)
              .IsRequired()
              .HasMaxLength(255)
              .HasColumnName("value");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");
    }
}