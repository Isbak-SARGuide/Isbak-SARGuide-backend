using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isbak_SAR_Guide.DataAccess.Configurations;

public class PublishedContentConfiguration : IEntityTypeConfiguration<PublishedContent>
{
    public void Configure(EntityTypeBuilder<PublishedContent> builder)
    {
        // Version=0/negatif hicbir yayinin anlami degil - BookPublication'daki
        // ayni kisitin karsiligi.
        builder.ToTable(t => t.HasCheckConstraint("CK_PublishedContents_Version", "\"Version\" > 0"));

        // json, jsonb DEGIL - bilerek: jsonb metni kanonikelestirir (key'leri
        // yeniden siralar, whitespace atar), Checksum = SHA256(PayloadJson)
        // invariant'i ise bayt sadakati ister. json metni aynen saklar + yazim
        // aninda gecerlilik dogrular. DataJson (draft, ContentBlock) jsonb
        // kalir - o yapisal veri, uzerinde checksum sozu yok. (6.5 testi yakaladi)
        builder.Property(pc => pc.PayloadJson).HasColumnType("json").IsRequired();
        builder.Property(pc => pc.Checksum).HasMaxLength(128).IsRequired();

        builder.HasOne(pc => pc.BookPublication)
            .WithMany(p => p.PublishedContents)
            .HasForeignKey(pc => pc.BookPublicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Mobil delta sorgusu icin: WHERE BookId = @id AND Version > @fromVersion
        builder.HasIndex(pc => new { pc.BookId, pc.Version });

        // "Content basina en son durum" (greatest-per-group) sorgusu icin -
        // PublicationRepository.GetLatestContentStatesAsync/GetChangedRowsSinceAsync
        // ikisi de WHERE BookId=@id AND ContentId=@id ORDER BY Version DESC LIMIT 1
        // seklinde korele bir alt sorgu calistiriyor (Publish/Rollback'in ortak
        // FinalizeAsync'i VE her mobil /sync/changes istegi). Bu index olmadan
        // Postgres yukaridaki (BookId, Version) index'ini kullanip ContentId'yi
        // satir satir filtreliyordu - canli EXPLAIN ANALYZE ile olculdu (989
        // satirda 14ms, 14.000 buffer hit); bu index'le ayni sorgu 2.4ms'e,
        // 3.070 buffer hit'e dustu (Index Only Scan, dogrudan (BookId, ContentId)
        // ile arama). Kritik: bu maliyet PublishedContents satir sayisiyla
        // (silinen satir yok, sadece birikir) DOGRUSAL DEGIL buyuyordu -
        // index'siz haliyle yayin gecmisi arttikca orantisiz kotulesirdi.
        builder.HasIndex(pc => new { pc.BookId, pc.ContentId, pc.Version })
            .IsDescending(false, false, true);

        // KASITLI OLARAK query filter YOK.
        // Buradaki IsDeleted "bu satiri admin listesinde gizle" anlamina
        // gelmiyor - "mobile bu icerigin silindigini bildir" (tombstone)
        // anlamina geliyor. Global soft-delete filtresi buraya uygulanirsa
        // silinen icerikler delta sorgusundan hic gorunmez, mobil silme
        // olayini asla ogrenemez. (Risk #6 - Yol Haritasi Bolum 12)
    }
}
