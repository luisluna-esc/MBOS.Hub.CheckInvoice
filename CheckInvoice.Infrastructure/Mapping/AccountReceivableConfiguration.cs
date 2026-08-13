using CheckInvoice.core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class AccountReceivableConfiguration : IEntityTypeConfiguration<AccountReceivable>
{
    public void Configure(EntityTypeBuilder<AccountReceivable> entity)
    {
        entity.ToTable("account_receivable");

        entity.HasKey(e => e.AccountReceivableId);
        entity.Property(e => e.AccountReceivableId)
              .HasColumnName("account_receivable_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.IssueId)
              .HasColumnName("issue_id");

        entity.Property(e => e.ClientId)
              .HasColumnName("client_id");

        entity.Property(e => e.TotalAmount)
              .IsRequired()
              .HasColumnName("total_amount")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.OutstandingBalance)
              .IsRequired()
              .HasColumnName("outstanding_balance")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.PaymentType)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("payment_type");

        entity.Property(e => e.DueDate)
              .HasColumnName("due_date")
              .HasColumnType("date");

        entity.Property(e => e.Status)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("status");

        entity.Property(e => e.CreatedAt)
              .IsRequired()
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");
    }
}