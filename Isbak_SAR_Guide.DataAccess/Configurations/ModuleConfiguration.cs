using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isbak_SAR_Guide.DataAccess.Configurations;

public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.Description).HasMaxLength(2000);

        builder.HasOne(m => m.Book)
            .WithMany(b => b.Modules)
            .HasForeignKey(m => m.BookId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique + partial (WHERE NOT IsDeleted): iki modul ayni kitapta
        // ayni pozisyonu iddia edemez. Faz 5'teki reorder endpoint'i bu
        // yuzden iki-fazli calismali (once gecici/negatif DisplayOrder'lara
        // tasi, sonra final degerlere yaz) - tek adimda "B'yi C'nin yerine
        // koy" yapmaya calismak gecici bir cakismaya (constraint ihlali)
        // takilir.
        builder.HasIndex(m => new { m.BookId, m.DisplayOrder })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
