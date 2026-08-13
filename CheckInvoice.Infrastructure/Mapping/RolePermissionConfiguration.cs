using CheckInvoice.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> entity)
    {
        entity.ToTable("role_permission");

        entity.HasKey(e => e.RolePermissionId);
        entity.Property(e => e.RolePermissionId)
              .HasColumnName("role_permission_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.RoleId)
              .IsRequired()
              .HasColumnName("role_id");

        entity.Property(e => e.PermissionId)
              .IsRequired()
              .HasColumnName("permission_id");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.CreatedById)
              .HasColumnName("created_by");
    }
}