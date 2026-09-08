using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class VoidReasonConfiguration : IEntityTypeConfiguration<VoidReason>
{
    public void Configure(EntityTypeBuilder<VoidReason> entity)
    {
        entity.ToTable("void_reason");

        entity.HasKey(e => e.VoidReasonId);
        entity.Property(e => e.VoidReasonId)
              .HasColumnName("void_reason_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Code)
              .IsRequired()
              .HasMaxLength(20)
              .HasColumnName("code");

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.HasIndex(e => e.Code).IsUnique();
    }
}
