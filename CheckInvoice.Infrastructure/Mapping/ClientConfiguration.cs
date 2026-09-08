using CheckInvoice.core.Entities.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> entity)
    {
        entity.ToTable("client");

        entity.HasKey(e => e.ClientId);
        entity.Property(e => e.ClientId)
              .HasColumnName("client_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.PartyId)
              .IsRequired()
              .HasColumnName("party_id");

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.DocumentTypeId)
              .HasColumnName("document_type_id");

        entity.Property(e => e.DistrictId)
              .HasColumnName("district_id");

        entity.Property(e => e.ChurchId)
              .HasColumnName("church_id");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.Complement)
              .HasMaxLength(5)
              .HasColumnName("complement");

        entity.Property(e => e.SpecialCaseId)
              .HasColumnName("special_case_id");

        entity.Property(e => e.IsPastor)
              .IsRequired()
              .HasDefaultValue(false)
              .HasColumnName("is_pastor");

        entity.HasIndex(e => e.AppUserId).IsUnique();
        entity.HasIndex(e => e.PartyId).IsUnique();
    }
}
