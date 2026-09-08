using CheckInvoice.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class MenuRoleConfiguration : IEntityTypeConfiguration<MenuRole>
{
    public void Configure(EntityTypeBuilder<MenuRole> entity)
    {
        entity.ToTable("menu_role");

        entity.HasKey(e => e.MenuRoleId);
        entity.Property(e => e.MenuRoleId)
              .HasColumnName("menu_role_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.MenuId)
              .IsRequired()
              .HasColumnName("menu_id");

        entity.Property(e => e.RoleId)
              .IsRequired()
              .HasColumnName("role_id");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");
    }
}
