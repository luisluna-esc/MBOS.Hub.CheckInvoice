using CheckInvoice.core.Entities.Catalogs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class SubDepartmentConfiguration : IEntityTypeConfiguration<SubDepartment>
{
    public void Configure(EntityTypeBuilder<SubDepartment> entity)
    {
        entity.ToTable("sub_department");

        entity.HasKey(e => e.SubDepartmentId);
        entity.Property(e => e.SubDepartmentId)
              .HasColumnName("sub_department_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.Name)
              .IsRequired()
              .HasMaxLength(100)
              .HasColumnName("name");

        entity.Property(e => e.IsActive)
              .HasColumnName("is_active");
    }
}