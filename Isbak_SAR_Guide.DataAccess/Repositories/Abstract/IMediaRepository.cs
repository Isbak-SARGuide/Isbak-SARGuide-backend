using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

public interface IMediaRepository : IRepository<Media>
{
    /// <summary>Dedup icin: ayni SHA-256'ya sahip bir Media zaten var mi (Checksum UNIQUE).</summary>
    Task<Media?> FindByChecksumAsync(string checksum, CancellationToken cancellationToken = default);

    /// <summary>
    /// Hicbir ContentBlock'un referans vermedigi VE en az `olderThanUtc`'den
    /// once yuklenmis Media satirlari (Faz 6.6 - yetim temizligi). Yas siniri:
    /// az once yuklenip henuz bir ContentBlock'a baglanmamis (istek hala devam
    /// ediyor) bir medyanin yaris durumunda silinmesini onler.
    /// </summary>
    Task<IReadOnlyList<Media>> FindOrphansAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default);
}
