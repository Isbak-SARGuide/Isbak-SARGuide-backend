using AramaKurtarma.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AramaKurtarma.DataAccess.Configurations;

public class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.Property(m => m.FileName).HasMaxLength(260).IsRequired();
        builder.Property(m => m.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(m => m.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Checksum).HasMaxLength(128).IsRequired();

        builder.HasIndex(m => m.Checksum);

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
