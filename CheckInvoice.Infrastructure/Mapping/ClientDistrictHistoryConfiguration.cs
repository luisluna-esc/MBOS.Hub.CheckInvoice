using CheckInvoice.core.Entities.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class ClientDistrictHistoryConfiguration : IEntityTypeConfiguration<ClientDistrictHistory>
{
    public void Configure(EntityTypeBuilder<ClientDistrictHistory> entity)
    {
        entity.ToTable("client_district_history");

        entity.HasKey(e => e.ClientDistrictHistoryId);
        entity.Property(e => e.ClientDistrictHistoryId)
              .HasColumnName("client_district_history_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.ClientId)
              .IsRequired()
              .HasColumnName("client_id");

        entity.Property(e => e.DistrictId)
              .IsRequired()
              .HasColumnName("district_id");

        entity.Property(e => e.StartDate)
              .IsRequired()
              .HasColumnName("start_date")
              .HasColumnType("date");

        entity.Property(e => e.EndDate)
              .HasColumnName("end_date")
              .HasColumnType("date");
    }
}