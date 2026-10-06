using CheckInvoice.core.Entities.Finance;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PaymentDetailConfiguration : IEntityTypeConfiguration<PaymentDetail>
{
    public void Configure(EntityTypeBuilder<PaymentDetail> entity)
    {
        entity.ToTable("payment_detail");

        entity.HasKey(e => e.PaymentDetailId);
        entity.Property(e => e.PaymentDetailId)
              .HasColumnName("payment_detail_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.PaymentId)
              .IsRequired()
              .HasColumnName("payment_id");

        entity.Property(e => e.IssueDetailId)
              .IsRequired()
              .HasColumnName("issue_detail_id");

        entity.Property(e => e.ProductId)
              .IsRequired()
              .HasColumnName("product_id");

        entity.Property(e => e.Amount)
              .IsRequired()
              .HasColumnName("amount")
              .HasColumnType("numeric(12,2)");
    }
}
