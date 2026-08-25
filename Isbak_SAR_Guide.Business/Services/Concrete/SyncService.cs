using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Sync;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

/// <summary>
/// Manifest ve snapshot yayin tablolarindan VERBATIM okunur (7.1/7.2) -
/// uretici publish, sync artik sadece okuyucu. Yalnizca GetChangesAsync
/// stub'tir (7.3'e kadar). Karisik donem bilerek bu feature branch'ine
/// hapsedilmistir - 7.1-7.3 tek PR olarak iner, develop/main hep tutarli.
/// </summary>
public class SyncService(IUnitOfWork unitOfWork) : ISyncService
{
    public async Task<Result<string>> GetManifestAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var manifestJson = await unitOfWork.Publications.GetLatestManifestJsonAsync(bookId, cancellationToken);

        return manifestJson is not null
            ? Result.Success(manifestJson)
            : Result.Failure<string>(await ResolveNotFoundAsync(bookId, cancellationToken));
    }

    public async Task<Result<string>> GetSnapshotAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var snapshotJson = await unitOfWork.Publications.GetLatestSnapshotJsonAsync(bookId, cancellationToken);

        return snapshotJson is not null
            ? Result.Success(snapshotJson)
            : Result.Failure<string>(await ResolveNotFoundAsync(bookId, cancellationToken));
    }

    public Task<Result<SyncChangesDto>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default)
    {
        // STUB: gercek delta hesabi PublishedContent.Version uzerinden 7.3-c'de gelecek.
        var changes = new SyncChangesDto(fromVersion, fromVersion, [], [], [], [], []);
        return Task.FromResult(Result.Success(changes));
    }

    /// <summary>
    /// Yayin bulunamayinca iki durumu ayirir - tek ek PK sorgusuyla, yalnizca
    /// bu yolda (happy path etkilenmez): yanlis id (konfigurasyon hatasi) mi,
    /// henuz yayinlanmamis kitap (mesru "icerik hazirlaniyor" durumu) mu?
    /// Kodlar TUM sync uclari icin ortak - kod, ucun degil gercegin adi;
    /// bu yardimci ortak oldugu icin ayrisamaz da.
    /// </summary>
    private async Task<Error> ResolveNotFoundAsync(int bookId, CancellationToken cancellationToken)
    {
        var book = await unitOfWork.Books.FindByIdAsync(bookId, cancellationToken);

        return book is null
            ? Error.NotFound("Sync.BookNotFound", $"Id={bookId} olan kitap bulunamadı.")
            : Error.NotFound("Sync.NotPublished", "Kitap henüz yayınlanmadı.");
    }
}
