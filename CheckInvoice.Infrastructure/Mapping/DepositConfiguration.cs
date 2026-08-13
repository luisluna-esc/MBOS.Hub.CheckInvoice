using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class DepositConfiguration : IEntityTypeConfiguration<Deposit>
{
    public void Configure(EntityTypeBuilder<Deposit> entity)
    {
        entity.ToTable("deposit");

        entity.HasKey(e => e.DepositId);
        entity.Property(e => e.DepositId)
              .HasColumnName("deposit_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.ClientId)
              .HasColumnName("client_id");

        entity.Property(e => e.ProductId)
              .HasColumnName("product_id");

        entity.Property(e => e.ShipmentId)
              .HasColumnName("shipment_id");

        entity.Property(e => e.ReceiptNumber)
              .HasMaxLength(20)
              .HasColumnName("receipt_number");

        entity.Property(e => e.DepositDate)
              .IsRequired()
              .HasColumnName("deposit_date")
              .HasColumnType("date");

        entity.Property(e => e.Amount)
              .IsRequired()
              .HasColumnName("amount")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.Notes)
              .HasMaxLength(255)
              .HasColumnName("notes");
    }
}