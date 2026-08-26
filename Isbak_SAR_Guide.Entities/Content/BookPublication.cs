using Isbak_SAR_Guide.Entities.Identity;

namespace Isbak_SAR_Guide.Entities.Content;

/// <summary>
/// Bir kitabin tek bir yayin anini temsil eder. Immutable - hic guncellenmez,
/// hic silinmez. Yayin gecmisi ve rollback bu satirlar sayesinde mumkun olur.
/// </summary>
public class BookPublication
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public int Version { get; set; }

    /// <summary>
    /// Manifest payload'i (json - bilerek jsonb degil, bayt sadakati icin) -
    /// mobil /sync/manifest bu json'dan uretilir.
    /// </summary>
    public string ManifestJson { get; set; } = null!;

    /// <summary>
    /// Yayinin tam kanonik snapshot'i (SyncSnapshotDto JSON'u). GetSnapshot bu
    /// kolonu deserialize etmeden AYNEN doner (verbatim). Checksum = SHA256(bu
    /// kolonun aynen kendisi). Silinen content'ler burada YOKTUR - tombstone
    /// yalnizca PublishedContent kavramidir; snapshot yeni istemcinin dunyasidir.
    /// Icerik PublishedContent satirlarinda da var - bilincli tekrar: immutable
    /// verinin kopyasi drift edemez, roller ayri (satirlar delta'nin,
    /// SnapshotJson ilk kurulumun kaynagi).
    /// </summary>
    public string SnapshotJson { get; set; } = null!;

    public string Checksum { get; set; } = null!;

    public DateTime PublishedAt { get; set; }

    public string PublishedById { get; set; } = null!;

    public Book Book { get; set; } = null!;

    public ApplicationUser PublishedBy { get; set; } = null!;

    public ICollection<PublishedContent> PublishedContents { get; set; } = new List<PublishedContent>();
}
