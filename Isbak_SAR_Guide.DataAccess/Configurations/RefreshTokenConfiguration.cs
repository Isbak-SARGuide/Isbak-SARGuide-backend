using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isbak_SAR_Guide.DataAccess.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.Property(t => t.TokenHash).HasMaxLength(128).IsRequired();

        // Unique: iki farkli satirin ayni token'a sahip olmasi imkansiz olmali -
        // dedup degil, bir hash carpismasi/programlama hatasi burada patlamali.
        builder.HasIndex(t => t.TokenHash).IsUnique();

        // RevokeAllActiveForUserAsync (reuse tespiti) bu kombinasyonla sorgular.
        builder.HasIndex(t => new { t.UserId, t.RevokedAtUtc });

        builder.HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
