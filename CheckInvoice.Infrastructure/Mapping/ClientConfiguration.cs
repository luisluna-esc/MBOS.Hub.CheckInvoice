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

        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id");

        entity.Property(e => e.DocumentTypeId)
              .HasColumnName("document_type_id");

        entity.Property(e => e.TaxId)
              .HasMaxLength(20)
              .HasColumnName("tax_id");

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(150)
              .HasColumnName("name");

        entity.Property(e => e.Email)
              .HasMaxLength(150)
              .HasColumnName("email");

        entity.Property(e => e.MobilePhone)
              .HasMaxLength(20)
              .HasColumnName("mobile_phone");

        entity.Property(e => e.DistrictId)
              .HasColumnName("district_id");

        entity.Property(e => e.ChurchId)
              .HasColumnName("church_id");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.Complement)
              .HasMaxLength(10)
              .HasColumnName("complement");
    }
}