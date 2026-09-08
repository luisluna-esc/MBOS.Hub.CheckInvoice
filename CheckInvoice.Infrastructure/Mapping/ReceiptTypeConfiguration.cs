using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ReceiptTypeConfiguration : IEntityTypeConfiguration<ReceiptType>
{
    public void Configure(EntityTypeBuilder<ReceiptType> entity)
    {
        entity.ToTable("receipt_type");

        entity.HasKey(e => e.ReceiptTypeId);
        entity.Property(e => e.ReceiptTypeId)
              .HasColumnName("receipt_type_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("name");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");
    }
}