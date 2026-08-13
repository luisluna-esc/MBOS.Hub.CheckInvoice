using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class TransferDetailConfiguration : IEntityTypeConfiguration<TransferDetail>
{
    public void Configure(EntityTypeBuilder<TransferDetail> entity)
    {
        entity.ToTable("transfer_detail");

        entity.HasKey(e => e.TransferDetailId);
        entity.Property(e => e.TransferDetailId)
              .HasColumnName("transfer_detail_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.TransferId)
              .IsRequired()
              .HasColumnName("transfer_id");

        entity.Property(e => e.ProductId)
              .IsRequired()
              .HasColumnName("product_id");

        entity.Property(e => e.Quantity)
              .IsRequired()
              .HasColumnName("quantity")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.UnitPrice)
              .HasColumnName("unit_price")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.TotalSalePrice)
              .HasColumnName("total_sale_price")
              .HasColumnType("numeric(12,2)");
    }
}