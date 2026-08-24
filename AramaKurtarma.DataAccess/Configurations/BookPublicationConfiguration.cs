using AramaKurtarma.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AramaKurtarma.DataAccess.Configurations;

public class BookPublicationConfiguration : IEntityTypeConfiguration<BookPublication>
{
    public void Configure(EntityTypeBuilder<BookPublication> builder)
    {
        builder.Property(p => p.ManifestJson).HasColumnType("jsonb").IsRequired();
        builder.Property(p => p.Checksum).HasMaxLength(128).IsRequired();

        // Immutable denetim kaydi - Book veya kullanici silinse bile yayin
        // gecmisi kaybolmamali. Kasitli olarak Restrict.
        //
        // IsRequired(false): Book'ta soft-delete query filter'i var,
        // BookPublication'da YOK (kasitli - immutable audit). Iliski
        // "required" isaretli kalirsa EF, Book soft-delete edildiginde
        // Include(p => p.Book) davranisini belirsiz sayar ve uyari verir.
        // Optional isaretlemek u navigasyonu gevsetir, BookId hala normal
        // zorunlu bir int - sadece EF'in filtre-ile-join beklentisini
        // dogru sekilde ayarliyoruz. (Risk #6 - Yol Haritasi Bolum 12)
        builder.HasOne(p => p.Book)
            .WithMany()
            .HasForeignKey(p => p.BookId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.PublishedBy)
            .WithMany()
            .HasForeignKey(p => p.PublishedById)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(p => new { p.BookId, p.Version }).IsUnique();

        // NOT: BookPublication'da soft-delete kavrami yok, query filter YOK.
        // Immutable kayitlar zaten silinmez.
    }
}
