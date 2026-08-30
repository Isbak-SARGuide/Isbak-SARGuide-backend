using System.Text.Json;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.Mapping;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

/// <summary>
/// Manifest, snapshot ve degisiklikler (delta) yayin tablolarindan VERBATIM
/// okunur (7.1/7.2/7.3) - uretici publish, sync sadece okuyucu. Delta,
/// envelope'u elle yazan SyncChangesJsonWriter uzerinden uretilir; content
/// parcalari, modul listesi ve eklenen medya HAM KOPYADIR (WriteRawValue) -
/// donmus baytlara hicbir deserialize/re-serialize adimi dokunmaz.
/// </summary>
public class SyncService(IUnitOfWork unitOfWork) : ISyncService
{
    private static readonly Error _invalidFromVersionError = Error.Validation(
        "Sync.InvalidFromVersion", "Geçersiz sürüm numarası; tam senkronizasyon (snapshot) gerekli.");

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

    public async Task<Result<string>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default)
    {
        var currentVersion = await unitOfWork.Publications.GetLatestVersionAsync(bookId, cancellationToken);

        if (currentVersion == 0)
        {
            return Result.Failure<string>(await ResolveNotFoundAsync(bookId, cancellationToken));
        }

        if (fromVersion < 0 || fromVersion > currentVersion)
        {
            return Result.Failure<string>(_invalidFromVersionError);
        }

        string? previousManifestJson = null;

        if (fromVersion > 0)
        {
            previousManifestJson = await unitOfWork.Publications.GetManifestJsonAsync(bookId, fromVersion, cancellationToken);

            if (previousManifestJson is null)
            {
                // Defensive: immutable + bosluksuz versiyonlamada teorik
                // olarak imkansiz ama sessizce yanlis tahmin yerine
                // durustce 400 doner (fromVersion=0'da bu dal calismaz -
                // "hic yayin yok" orada mesru bos kume anlamina gelir).
                return Result.Failure<string>(_invalidFromVersionError);
            }
        }

        // Guvenli null-forgiving: currentVersion > 0, ayni tablonun
        // MAX(Version)'undan geliyor - en az bir yayin satiri var demektir.
        var currentSnapshotJson = (await unitOfWork.Publications.GetLatestSnapshotJsonAsync(bookId, cancellationToken))!;
        var currentManifestJson = (await unitOfWork.Publications.GetLatestManifestJsonAsync(bookId, cancellationToken))!;

        var bookRawJson = ExtractRawProperty(currentSnapshotJson, "book");
        var modulesRawJson = ExtractRawProperty(currentSnapshotJson, "modules");
        var (addedMedia, removedMediaIds) = ComputeMediaDiff(previousManifestJson, currentManifestJson);

        // fromVersion == currentVersion icin ozel dal YOK: sorgu matematigi
        // hallediyor - Version > currentVersion hicbir satir bulamaz (bos
        // degisiklik), previousManifestJson == currentManifestJson oldugu
        // icin medya diff'i de kendiliginden bos cikar.
        var changedRows = await unitOfWork.Publications.GetChangedRowsSinceAsync(bookId, fromVersion, cancellationToken);

        var upsertedPayloads = changedRows.Where(r => !r.IsDeleted).Select(r => r.PayloadJson).ToList();
        var deletedContentIds = changedRows.Where(r => r.IsDeleted).Select(r => r.ContentId).ToList();

        var json = SyncChangesJsonWriter.Write(
            fromVersion, currentVersion, bookRawJson, upsertedPayloads, deletedContentIds, modulesRawJson, addedMedia, removedMediaIds);

        return Result.Success(json);
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

    private static string ExtractRawProperty(string snapshotJson, string propertyName)
    {
        using var document = JsonDocument.Parse(snapshotJson);
        return document.RootElement.GetProperty(propertyName).GetRawText();
    }

    /// <summary>
    /// Iki donmus manifest'in "media" dizilerini karsilastirir: yeni'de olup
    /// eskide olmayan veya checksum'i degisen -> added (guncel manifest'ten
    /// ham kopya); eskide olup yenide olmayan -> removed. previousManifestJson
    /// null ise (fromVersion=0) eski kume bos sayilir - tum medya "added" olur.
    /// </summary>
    private static (List<string> Added, List<int> RemovedIds) ComputeMediaDiff(
        string? previousManifestJson, string currentManifestJson)
    {
        var previousMedia = previousManifestJson is null
            ? new Dictionary<int, (string Checksum, string RawText)>()
            : ParseMediaById(previousManifestJson);
        var currentMedia = ParseMediaById(currentManifestJson);

        var added = currentMedia
            .Where(entry => !previousMedia.TryGetValue(entry.Key, out var previous) || previous.Checksum != entry.Value.Checksum)
            .OrderBy(entry => entry.Key)
            .Select(entry => entry.Value.RawText)
            .ToList();

        var removedIds = previousMedia.Keys
            .Where(id => !currentMedia.ContainsKey(id))
            .OrderBy(id => id)
            .ToList();

        return (added, removedIds);
    }

    private static Dictionary<int, (string Checksum, string RawText)> ParseMediaById(string manifestJson)
    {
        using var document = JsonDocument.Parse(manifestJson);
        var result = new Dictionary<int, (string, string)>();

        foreach (var item in document.RootElement.GetProperty("media").EnumerateArray())
        {
            var id = item.GetProperty("id").GetInt32();
            var checksum = item.GetProperty("checksum").GetString()!;
            result[id] = (checksum, item.GetRawText());
        }

        return result;
    }
}
