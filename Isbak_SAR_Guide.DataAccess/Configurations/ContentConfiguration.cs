using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isbak_SAR_Guide.DataAccess.Configurations;

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> builder)
    {
        builder.Property(c => c.Title).HasMaxLength(200).IsRequired();
        builder.Property(c => c.Summary).HasMaxLength(500);
        builder.Property(c => c.VariantGroupKey).HasMaxLength(100);
        builder.Property(c => c.VariantLabel).HasMaxLength(50);

        builder.HasOne(c => c.Module)
            .WithMany(m => m.Contents)
            .HasForeignKey(c => c.ModuleId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique + partial: ModuleConfiguration'daki ayni gerekce.
        builder.HasIndex(c => new { c.ModuleId, c.DisplayOrder })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        // Coglugu null olacak (cogu Content tekil) - sadece dolu satirlari
        // indeksle. Admin panelde "bu grubun digerlerini goster" gibi bir
        // sorgu ihtiyaci dogarsa hazir.
        builder.HasIndex(c => new { c.ModuleId, c.VariantGroupKey })
            .HasFilter("\"VariantGroupKey\" IS NOT NULL");

        builder.HasQueryFilter(c => !c.IsDeleted);
    }
}
