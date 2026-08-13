using CheckInvoice.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class MenuConfiguration : IEntityTypeConfiguration<Menu>
{
    public void Configure(EntityTypeBuilder<Menu> entity)
    {
        entity.ToTable("menu");

        entity.HasKey(e => e.MenuId);
        entity.Property(e => e.MenuId)
              .HasColumnName("menu_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.Property(e => e.Route)
              .HasMaxLength(150)
              .HasColumnName("route");

        entity.Property(e => e.Icon)
              .HasMaxLength(50)
              .HasColumnName("icon");

        entity.Property(e => e.ParentMenuId)
              .HasColumnName("parent_menu_id");

        entity.Property(e => e.DisplayOrder)
              .IsRequired()
              .HasColumnName("display_order");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.PermissionId)
              .HasColumnName("permission_id");
    }
}