using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Sync;
using Isbak_SAR_Guide.Business.Mapping;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

/// <summary>
/// GetManifestAsync GERCEK (7.1): yayin tablosundan verbatim okur.
/// GetSnapshotAsync/GetChangesAsync henuz STUB (5.0'dan): draft veriden
/// calisir, 7.2/7.3'te gercek okumalarla degisecek. Karisik donem bilerek
/// bu feature branch'ine hapsedilmistir - 7.1-7.3 tek PR olarak iner,
/// develop/main hep tutarli kalir.
/// </summary>
public class SyncService(IUnitOfWork unitOfWork) : ISyncService
{
    public async Task<Result<SyncSnapshotDto>> GetSnapshotAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var book = await unitOfWork.Books.GetWithFullTreeAsync(bookId, cancellationToken);

        if (book is null)
        {
            return Result.Failure<SyncSnapshotDto>(
                Error.NotFound("Sync.BookNotFound", $"Id={bookId} olan kitap bulunamadı."));
        }

        return Result.Success(SnapshotBuilder.BuildSnapshot(book));
    }

    public async Task<Result<string>> GetManifestAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var manifestJson = await unitOfWork.Publications.GetLatestManifestJsonAsync(bookId, cancellationToken);

        if (manifestJson is not null)
        {
            return Result.Success(manifestJson);
        }

        // Null'un iki sebebi var, tek ek PK sorgusuyla ayrilir (yalnizca bu
        // yolda - happy path etkilenmez): yanlis id (konfigurasyon hatasi) mi,
        // henuz yayinlanmamis kitap (mesru bos durum - "icerik hazirlaniyor") mu?
        var book = await unitOfWork.Books.FindByIdAsync(bookId, cancellationToken);

        return book is null
            ? Result.Failure<string>(
                Error.NotFound("Sync.BookNotFound", $"Id={bookId} olan kitap bulunamadı."))
            : Result.Failure<string>(
                Error.NotFound("Sync.NotPublished", "Kitap henüz yayınlanmadı."));
    }

    public Task<Result<SyncChangesDto>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default)
    {
        // STUB: gercek delta hesabi PublishedContent.Version uzerinden Faz 4'te gelecek.
        var changes = new SyncChangesDto(fromVersion, fromVersion, [], [], [], []);
        return Task.FromResult(Result.Success(changes));
    }
}
