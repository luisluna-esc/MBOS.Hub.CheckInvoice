using CheckInvoice.core.Entities.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> entity)
    {
        entity.ToTable("product_image");

        entity.HasKey(e => e.ProductId);
        entity.Property(e => e.ProductId)
              .HasColumnName("product_id")
              .ValueGeneratedNever();

        entity.Property(e => e.Content)
              .IsRequired()
              .HasColumnName("content")
              .HasColumnType("bytea");

        entity.Property(e => e.ContentType)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("content_type");

        entity.Property(e => e.UpdatedAt)
              .IsRequired()
              .HasColumnName("updated_at")
              .HasColumnType("timestamp without time zone");
    }
}
