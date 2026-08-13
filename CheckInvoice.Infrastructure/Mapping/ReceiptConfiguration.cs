using CheckInvoice.core.Entities.Movements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> entity)
    {
        entity.ToTable("receipt");

        entity.HasKey(e => e.ReceiptId);
        entity.Property(e => e.ReceiptId)
              .HasColumnName("receipt_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.SupplierId)
              .HasColumnName("supplier_id");

        entity.Property(e => e.TaxId)
              .HasMaxLength(20)
              .HasColumnName("tax_id");

        entity.Property(e => e.WarehouseId)
              .HasColumnName("warehouse_id");

        entity.Property(e => e.WarehousePeriodId)
              .HasColumnName("warehouse_period_id");

        entity.Property(e => e.ReceiptTypeId)
              .HasColumnName("receipt_type_id");

        entity.Property(e => e.InvoiceNumber)
              .HasMaxLength(50)
              .HasColumnName("invoice_number");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");

        entity.Property(e => e.IssueDate)
              .IsRequired()
              .HasColumnName("issue_date")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.InvoiceTotal)
              .HasColumnName("invoice_total")
              .HasColumnType("numeric(12,2)");
    }
}