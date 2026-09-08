using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class IssueDetailConfiguration : IEntityTypeConfiguration<IssueDetail>
{
    public void Configure(EntityTypeBuilder<IssueDetail> entity)
    {
        entity.ToTable("issue_detail");

        entity.HasKey(e => e.IssueDetailId);
        entity.Property(e => e.IssueDetailId)
              .HasColumnName("issue_detail_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.IssueId)
              .IsRequired()
              .HasColumnName("issue_id");

        entity.Property(e => e.ProductId)
              .IsRequired()
              .HasColumnName("product_id");

        entity.Property(e => e.Quantity)
              .IsRequired()
              .HasColumnName("quantity")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.UnitCost)
              .IsRequired()
              .HasColumnName("unit_cost")
              .HasColumnType("numeric(12,2)");

        entity.Property(e => e.TotalCost)
              .IsRequired()
              .HasColumnName("total_cost")
              .HasColumnType("numeric(12,2)");
    }
}