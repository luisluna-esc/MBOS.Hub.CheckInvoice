using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PrintTypeConfiguration : IEntityTypeConfiguration<PrintType>
{
    public void Configure(EntityTypeBuilder<PrintType> entity)
    {
        entity.ToTable("print_type");

        entity.HasKey(e => e.PrintTypeId);
        entity.Property(e => e.PrintTypeId)
              .HasColumnName("print_type_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("name");
    }
}