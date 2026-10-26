using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PendingDeliveryConfiguration : IEntityTypeConfiguration<PendingDelivery>
{
    public void Configure(EntityTypeBuilder<PendingDelivery> entity)
    {
        entity.ToTable("pending_delivery");

        entity.HasKey(e => e.PendingDeliveryId);
        entity.Property(e => e.PendingDeliveryId)
              .HasColumnName("pending_delivery_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.PendingReceiptId)
              .IsRequired()
              .HasColumnName("pending_receipt_id");

        entity.Property(e => e.DeliveryDate)
              .IsRequired()
              .HasColumnType("timestamp without time zone")
              .HasColumnName("delivery_date");

        entity.Property(e => e.IssueTypeId)
              .HasColumnName("issue_type_id");

        entity.Property(e => e.ClientId)
              .HasColumnName("client_id");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");

        entity.Property(e => e.SendToAccountsReceivable)
              .IsRequired()
              .HasColumnName("send_to_accounts_receivable");

        entity.Property(e => e.PaymentType)
              .HasMaxLength(20)
              .HasColumnName("payment_type");

        entity.Property(e => e.PaymentDetail)
              .HasMaxLength(255)
              .HasColumnName("payment_detail");

        entity.Property(e => e.DueDate)
              .HasColumnType("date")
              .HasColumnName("due_date");

        entity.Property(e => e.IsVoided)
              .IsRequired()
              .HasColumnName("is_voided");

        entity.Property(e => e.IssueId)
              .HasColumnName("issue_id");

        entity.Property(e => e.CreatedAt)
              .IsRequired()
              .HasColumnType("timestamp without time zone")
              .HasColumnName("created_at");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");
    }
}
