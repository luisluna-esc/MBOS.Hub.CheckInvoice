using CheckInvoice.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> entity)
    {
        entity.ToTable("permission");

        entity.HasKey(e => e.PermissionId);
        entity.Property(e => e.PermissionId)
              .HasColumnName("permission_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Code)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("code");

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.Property(e => e.Description)
              .HasMaxLength(255)
              .HasColumnName("description");

        entity.Property(e => e.Module)
              .IsRequired()
              .HasMaxLength(50)
              .HasColumnName("module");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");
    }
}