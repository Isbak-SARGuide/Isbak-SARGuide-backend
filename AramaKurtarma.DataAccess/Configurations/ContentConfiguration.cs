using AramaKurtarma.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AramaKurtarma.DataAccess.Configurations;

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> builder)
    {
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Summary).HasMaxLength(500);

        builder.HasOne(c => c.Module)
            .WithMany(m => m.Contents)
            .HasForeignKey(c => c.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(c => new { c.ModuleId, c.DisplayOrder });

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
