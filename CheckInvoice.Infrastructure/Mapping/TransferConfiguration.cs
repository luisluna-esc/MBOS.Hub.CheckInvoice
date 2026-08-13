using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class TransferConfiguration : IEntityTypeConfiguration<Transfer>
{
    public void Configure(EntityTypeBuilder<Transfer> entity)
    {
        entity.ToTable("transfer");

        entity.HasKey(e => e.TransferId);
        entity.Property(e => e.TransferId)
              .HasColumnName("transfer_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.SourceWarehouseId)
              .HasColumnName("source_warehouse_id");

        entity.Property(e => e.DestinationWarehouseId)
              .HasColumnName("destination_warehouse_id");

        entity.Property(e => e.SenderUserId)
              .HasColumnName("sender_user_id");

        entity.Property(e => e.ReceiverUserId)
              .HasColumnName("receiver_user_id");

        entity.Property(e => e.TransferDate)
              .IsRequired()
              .HasColumnName("transfer_date")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.Notes)
              .HasColumnName("notes")
              .HasColumnType("text");
    }
}