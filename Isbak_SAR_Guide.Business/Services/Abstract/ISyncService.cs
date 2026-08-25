using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Sync;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface ISyncService
{
    /// <summary>
    /// Son yayinin ManifestJson'unu AYNEN doner (verbatim ham JSON).
    /// Deserialize/re-serialize YASAK - web serializer'in encoder'i kanonik
    /// formdan farkli (\uXXXX escape'leri geri getirir), round-trip baytlari
    /// bozar ve istemcinin checksum dogrulamasini kirar.
    /// Hata kodlari: kitap yoksa Sync.BookNotFound, kitap var ama hic
    /// yayinlanmamissa Sync.NotPublished (ikisi de 404; mobil koda bakarak
    /// "yanlis id" ile "icerik hazirlaniyor"u ayirt eder).
    /// </summary>
    Task<Result<string>> GetManifestAsync(int bookId, CancellationToken cancellationToken = default);

    Task<Result<SyncSnapshotDto>> GetSnapshotAsync(int bookId, CancellationToken cancellationToken = default);

    Task<Result<SyncChangesDto>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default);
}
