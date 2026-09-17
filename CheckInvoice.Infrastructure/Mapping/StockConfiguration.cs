using CheckInvoice.core.Entities.Warehouses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class StockConfiguration : IEntityTypeConfiguration<Stock>
{
    public void Configure(EntityTypeBuilder<Stock> entity)
    {
        entity.ToTable("stock");

        entity.HasKey(e => e.StockId);
        entity.Property(e => e.StockId)
              .HasColumnName("stock_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.WarehouseId)
              .IsRequired()
              .HasColumnName("warehouse_id");

        entity.Property(e => e.ProductId)
              .IsRequired()
              .HasColumnName("product_id");

        entity.Property(e => e.Quantity)
              .IsRequired()
              .HasColumnName("quantity")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.AverageCost)
              .IsRequired()
              .HasColumnName("average_cost")
              .HasColumnType("numeric(12,4)");

        entity.HasIndex(e => new { e.WarehouseId, e.ProductId }).IsUnique();
    }
}