using AramaKurtarma.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AramaKurtarma.DataAccess.Configurations;

public class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        builder.Property(cb => cb.DataJson).HasColumnType("jsonb");

        builder.HasOne(cb => cb.Content)
            .WithMany(c => c.Blocks)
            .HasForeignKey(cb => cb.ContentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Media silinirse blok kaybolmasin - referans NULL'a duser.
        builder.HasOne(cb => cb.Media)
            .WithMany()
            .HasForeignKey(cb => cb.MediaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(cb => new { cb.ContentId, cb.DisplayOrder });

        builder.HasQueryFilter(cb => !cb.IsDeleted);
    }
}
