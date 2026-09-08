using CheckInvoice.core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class InstallmentConfiguration : IEntityTypeConfiguration<Installment>
{
    public void Configure(EntityTypeBuilder<Installment> entity)
    {
        entity.ToTable("installment");

        entity.HasKey(e => e.InstallmentId);
        entity.Property(e => e.InstallmentId)
              .HasColumnName("installment_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.AccountReceivableId)
              .IsRequired()
              .HasColumnName("account_receivable_id");

        entity.Property(e => e.InstallmentNumber)
              .IsRequired()
              .HasColumnName("installment_number");

        entity.Property(e => e.InstallmentAmount)
              .IsRequired()
              .HasColumnName("installment_amount")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.DueDate)
              .IsRequired()
              .HasColumnName("due_date")
              .HasColumnType("date");

        entity.Property(e => e.Status)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("status");
    }
}