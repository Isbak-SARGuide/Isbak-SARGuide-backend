using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isbak_SAR_Guide.DataAccess.Configurations;

public class PublishedContentConfiguration : IEntityTypeConfiguration<PublishedContent>
{
    public void Configure(EntityTypeBuilder<PublishedContent> builder)
    {
        builder.Property(pc => pc.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(pc => pc.Checksum).HasMaxLength(128).IsRequired();

        builder.HasOne(pc => pc.BookPublication)
            .WithMany(p => p.PublishedContents)
            .HasForeignKey(pc => pc.BookPublicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mobil delta sorgusu icin: WHERE BookId = @id AND Version > @fromVersion
        builder.HasIndex(pc => new { pc.BookId, pc.Version });

        // KASITLI OLARAK query filter YOK.
        // Buradaki IsDeleted "bu satiri admin listesinde gizle" anlamina
        // gelmiyor - "mobile bu icerigin silindigini bildir" (tombstone)
        // anlamina geliyor. Global soft-delete filtresi buraya uygulanirsa
        // silinen icerikler delta sorgusundan hic gorunmez, mobil silme
        // olayini asla ogrenemez. (Risk #6 - Yol Haritasi Bolum 12)
    }
}
