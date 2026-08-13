using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class DiscountConfiguration : IEntityTypeConfiguration<Discount>
{
    public void Configure(EntityTypeBuilder<Discount> entity)
    {
        entity.ToTable("discount");

        entity.HasKey(e => e.DiscountId);
        entity.Property(e => e.DiscountId)
              .HasColumnName("discount_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.ClientId)
              .HasColumnName("client_id");

        entity.Property(e => e.SourceTable)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("source_table");

        entity.Property(e => e.SourceId)
              .IsRequired()
              .HasColumnName("source_id");

        entity.Property(e => e.Amount)
              .IsRequired()
              .HasColumnName("amount")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");

        entity.Property(e => e.DiscountDate)
              .IsRequired()
              .HasColumnName("discount_date")
              .HasColumnType("date");

        entity.Property(e => e.Status)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("status");
    }
}