using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ChurchTypeConfiguration : IEntityTypeConfiguration<ChurchType>
{
    public void Configure(EntityTypeBuilder<ChurchType> entity)
    {
        entity.ToTable("church_type");

        entity.HasKey(e => e.ChurchTypeId);
        entity.Property(e => e.ChurchTypeId)
              .HasColumnName("church_type_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("name");

        entity.HasIndex(e => e.Name).IsUnique();
    }
}