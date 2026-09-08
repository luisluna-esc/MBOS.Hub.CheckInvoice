using CheckInvoice.core.Entities.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> entity)
    {
        entity.ToTable("product");

        entity.HasKey(e => e.ProductId);
        entity.Property(e => e.ProductId)
              .HasColumnName("product_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Code)
              .HasMaxLength(20)
              .HasColumnName("code");

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(200)
              .HasColumnName("name");

        entity.Property(e => e.DepartmentId)
              .HasColumnName("department_id");

        entity.Property(e => e.SubDepartmentId)
              .HasColumnName("sub_department_id");

        entity.Property(e => e.MediaTypeId)
              .HasColumnName("media_type_id");

        entity.Property(e => e.Price)
              .HasColumnName("price")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");
    }
}