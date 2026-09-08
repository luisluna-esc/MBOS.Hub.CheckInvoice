using CheckInvoice.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> entity)
    {
        entity.ToTable("app_user");

        entity.HasKey(e => e.AppUserId);
        entity.Property(e => e.AppUserId)
              .HasColumnName("app_user_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.FirstName)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("first_name");

        entity.Property(e => e.LastName)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("last_name");

        entity.Property(e => e.Email)
              .IsRequired()
              .HasMaxLength(150)
              .HasColumnName("email");

        entity.Property(e => e.Username)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("username");

        entity.Property(e => e.PasswordHash)
              .IsRequired()
              .HasMaxLength(255)
              .HasColumnName("password_hash");

        entity.Property(e => e.LastLogin)
              .HasColumnName("last_login")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");

        entity.Property(e => e.UpdatedAt)
              .HasColumnName("updated_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.UpdatedById)
              .HasColumnName("updated_by");
    }
}