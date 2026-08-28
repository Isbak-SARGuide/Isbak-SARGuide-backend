using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isbak_SAR_Guide.DataAccess.Configurations;

public class ContentBlockConfiguration : IEntityTypeConfiguration<ContentBlock>
{
    public void Configure(EntityTypeBuilder<ContentBlock> builder)
    {
        // Type, C# tarafinda ContentBlockType enum'u (1-6) ama DB'de duz
        // integer - bu CHECK olmadan elle SQL/import script'i gecerli
        // olmayan bir deger yazabilir (DB hicbir sey demez, uygulama
        // okurken patlar). Enum'a deger eklenirse bu kisit da guncellenmeli.
        builder.ToTable(t => t.HasCheckConstraint("CK_ContentBlocks_Type", "\"Type\" BETWEEN 1 AND 6"));

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
