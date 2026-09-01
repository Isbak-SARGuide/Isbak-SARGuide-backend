using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Isbak_SAR_Guide.DataAccess.Configurations;

public class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        // MediaType (1-4) icin ayni gerekce: ContentBlockType CHECK'ine bakin.
        builder.ToTable(t => t.HasCheckConstraint("CK_Media_MediaType", "\"MediaType\" BETWEEN 1 AND 4"));

        builder.Property(m => m.FileName).HasMaxLength(260).IsRequired();
        builder.Property(m => m.StoragePath).HasMaxLength(500).IsRequired();
        builder.Property(m => m.ThumbnailStoragePath).HasMaxLength(500);
        builder.Property(m => m.ContentType).HasMaxLength(100).IsRequired();
        builder.Property(m => m.Checksum).HasMaxLength(128).IsRequired();

        // Unique: ayni dosyanin iki kez import edilmesi sessizce iki Media
        // satiri uretmesin - checksum zaten icerigin kimligi, tekilligi
        // DB'de garanti edilir (dedup). PARTIAL (Module/Content'in
        // (ParentId, DisplayOrder) indexiyle ayni desen, ContentConfiguration/
        // ModuleConfiguration): soft-delete edilen bir Media'nin checksum'i
        // tabloyu sonsuza kadar isgal ETMEMELI. Partial olmadan onceki halde,
        // ayni icerik soft-delete sonrasi tekrar yuklendiginde unique
        // violation'a carpiyordu, "eszamanli yukleme yarisi" kurtarma yolu
        // devreye giriyordu, ama FindByChecksumAsync da soft-delete filtresine
        // tabi oldugu icin "kazanan" satiri hic bulamiyordu - sonuc, kalici
        // (retry'la duzelmeyen) Media.ConcurrentUploadUnresolved 500'u
        // (Backend-Yapilacaklar.md #5, canli olarak izole edilip kanitlandi).
        builder.HasIndex(m => m.Checksum)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder.HasQueryFilter(m => !m.IsDeleted);
    }
}
