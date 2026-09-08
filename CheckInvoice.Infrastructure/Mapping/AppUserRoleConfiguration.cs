using CheckInvoice.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class AppUserRoleConfiguration : IEntityTypeConfiguration<AppUserRole>
{
    public void Configure(EntityTypeBuilder<AppUserRole> entity)
    {
        entity.ToTable("app_user_role");

        entity.HasKey(e => e.AppUserRoleId);
        entity.Property(e => e.AppUserRoleId)
              .HasColumnName("app_user_role_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.AppUserId)
              .IsRequired()
              .HasColumnName("app_user_id");

        entity.Property(e => e.RoleId)
              .IsRequired()
              .HasColumnName("role_id");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");
    }
}