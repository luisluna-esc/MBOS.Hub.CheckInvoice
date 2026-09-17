using CheckInvoice.core.Entities.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> entity)
    {
        entity.ToTable("party");

        entity.HasKey(e => e.PartyId);
        entity.Property(e => e.PartyId)
              .HasColumnName("party_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(150)
              .HasColumnName("name");

        entity.Property(e => e.TaxId)
              .HasMaxLength(20)
              .HasColumnName("tax_id");

        entity.Property(e => e.Email)
              .HasMaxLength(150)
              .HasColumnName("email");

        entity.Property(e => e.MobilePhone)
              .HasMaxLength(20)
              .HasColumnName("mobile_phone");
    }
}
