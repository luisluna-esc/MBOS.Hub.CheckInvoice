using CheckInvoice.core.Entities.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> entity)
    {
        entity.ToTable("supplier");

        entity.HasKey(e => e.SupplierId);
        entity.Property(e => e.SupplierId)
              .HasColumnName("supplier_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.PartyId)
              .IsRequired()
              .HasColumnName("party_id");

        entity.Property(e => e.Code)
              .HasMaxLength(20)
              .HasColumnName("code");

        entity.Property(e => e.LegalName)
              .HasMaxLength(150)
              .HasColumnName("legal_name");

        entity.Property(e => e.CountryId)
              .HasColumnName("country_id");

        entity.Property(e => e.Address)
              .HasMaxLength(255)
              .HasColumnName("address");

        entity.Property(e => e.Phone)
              .HasMaxLength(20)
              .HasColumnName("phone");

        entity.Property(e => e.Notes)
              .HasMaxLength(255)
              .HasColumnName("notes");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.HasIndex(e => e.PartyId).IsUnique();
    }
}
