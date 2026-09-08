using CheckInvoice.core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> entity)
    {
        entity.ToTable("payment");

        entity.HasKey(e => e.PaymentId);
        entity.Property(e => e.PaymentId)
              .HasColumnName("payment_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.AccountReceivableId)
              .IsRequired()
              .HasColumnName("account_receivable_id");

        entity.Property(e => e.InstallmentId)
              .HasColumnName("installment_id");

        entity.Property(e => e.Amount)
              .IsRequired()
              .HasColumnName("amount")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.PaymentDate)
              .IsRequired()
              .HasColumnName("payment_date")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.PaymentMethod)
              .HasMaxLength(30)
              .HasColumnName("payment_method");

        entity.Property(e => e.Notes)
              .HasMaxLength(255)
              .HasColumnName("notes");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");
    }
}