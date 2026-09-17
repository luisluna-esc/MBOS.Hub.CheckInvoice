using CheckInvoice.core.Entities.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CheckInvoice.Infrastructure.Mapping;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> entity)
    {
        entity.ToTable("refresh_token");

        entity.HasKey(e => e.RefreshTokenId);
        entity.Property(e => e.RefreshTokenId)
              .HasColumnName("refresh_token_id")
              .ValueGeneratedOnAdd();

        entity.Property(e => e.AppUserId)
              .IsRequired()
              .HasColumnName("app_user_id");

        entity.Property(e => e.Token)
              .IsRequired()
              .HasMaxLength(500)
              .HasColumnName("token");

        entity.Property(e => e.ExpiresAt)
              .IsRequired()
              .HasColumnName("expires_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.IsRevoked)
              .HasColumnName("is_revoked");

        entity.Property(e => e.CreatedAt)
              .HasColumnName("created_at")
              .HasColumnType("timestamp without time zone");

        entity.Property(e => e.CreatedIp)
              .HasMaxLength(50)
              .HasColumnName("created_ip");
    }
}