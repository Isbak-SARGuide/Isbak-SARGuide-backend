using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Publishing;
using Isbak_SAR_Guide.Business.DTOs.Sync;
using Isbak_SAR_Guide.Business.Mapping;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Common;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class PublishingService(IUnitOfWork unitOfWork, ILogger<PublishingService> logger) : IPublishingService
{
    /// <summary>
    /// Tombstone satirinin payload'i: icerik artik yok, kimligi ContentId
    /// kolonu tasiyor - son bilinen payload'i tasimak hem bayt israfi hem
    /// anlam bozuklugu olurdu. Bos obje, jsonb NOT NULL kisitini da karsilar.
    /// </summary>
    private const string _tombstonePayload = "{}";

    private readonly IUnitOfWork _unitOfWork = unitOfWork;

    public async Task<Result<PublishResultDto>> PublishAsync(
        int bookId,
        string publishedById,
        CancellationToken cancellationToken = default)
    {
        // Transaction'dan ONCE - kitap yoksa bosuna transaction acilmasin.
        var book = await _unitOfWork.Books.GetWithFullTreeAsync(bookId, cancellationToken);

        if (book is null)
        {
            return Result.Failure<PublishResultDto>(
                Error.NotFound("Publishing.BookNotFound", $"Id={bookId} olan kitap bulunamadı."));
        }

        // Gercek bir kez hesaplanir, sonra akar: ayni an hem manifest'e hem
        // PublishedAt kolonuna gider - iki UtcNow cagrisi iki farkli gercek olurdu.
        var publishedAt = DateTime.UtcNow;

        // await using: commit edilmeden scope'tan cikilirsa dispose otomatik
        // rollback yapar - ayrica catch { Rollback } gerekmez.
        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var latestVersion = await _unitOfWork.Publications.GetLatestVersionAsync(bookId, cancellationToken);
        var newVersion = latestVersion + 1;

        // Snapshot kurulmadan ONCE bump: BuildSnapshot DTO'ya book.Version'i
        // yazar. Entity zaten izleniyor (tracked) - SaveChanges farki gorur.
        book.Version = newVersion;

        var snapshot = SnapshotBuilder.BuildSnapshot(book);
        var publication = BuildPublicationShell(bookId, newVersion, snapshot, publishedAt, publishedById);

        // Journal modeli (7.3-a): satir tablosu tam kopya DEGIL, degisiklik
        // gunlugu - tam durum SnapshotJson'da. Her yayinda tum content'leri
        // yeniden yazmak deltayi tam indirmeye esitlerdi. Content basina en
        // son durum tek sorguyla gelir; hem "degisti mi?" kontrolu hem
        // tombstone diff'i bundan beslenir.
        var latestStates = await _unitOfWork.Publications
            .GetLatestContentStatesAsync(bookId, cancellationToken);

        AppendChangedContents(publication, snapshot, latestStates, bookId, newVersion);
        AppendTombstones(publication, snapshot, latestStates, bookId, newVersion);

        await _unitOfWork.Publications.AddAsync(publication, cancellationToken);

        // Dar try: sadece yazma/commit adimi. Baska bir satirin exception'i
        // yanlislikla Conflict kiligina girmesin. "when" filtresi sayesinde
        // unique ihlali OLMAYAN DbUpdateException hic yakalanmaz, oldugu gibi
        // global handler'a akar (bilmedigin hatayi yutma).
        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            // Iki eszamanli publish ayni versiyonu yazmaya kalkti - beklenen
            // bir yaris, exception degil Result doner. Bellekteki book.Version
            // bump'i sorun degil: context scoped, istek sonunda oluyor.
            logger.LogInformation(ex, "Kitap {BookId} icin eszamanli yayin yarisi - versiyon {Version} zaten yazilmis.", bookId, newVersion);
            return Result.Failure<PublishResultDto>(
                Error.Conflict(
                    "Publishing.VersionConflict",
                    "Aynı anda başka bir yayın yapıldı, lütfen tekrar deneyin."));
        }

        return Result.Success(new PublishResultDto(
            publication.Id, // EF, insert'te uretilen id'yi SaveChanges sonrasi geri doldurur
            bookId,
            newVersion,
            snapshot.Contents.Count,
            publication.Checksum,
            publishedAt));
    }

    public async Task<Result<PublishResultDto>> RollbackAsync(
        int bookId,
        int toVersion,
        string publishedById,
        CancellationToken cancellationToken = default)
    {
        var book = await _unitOfWork.Books.FindByIdAsync(bookId, cancellationToken);
        if (book is null)
        {
            return Result.Failure<PublishResultDto>(
                Error.NotFound("Publishing.BookNotFound", $"Id={bookId} olan kitap bulunamadı."));
        }

        var oldSnapshotJson = await _unitOfWork.Publications.GetSnapshotJsonAsync(bookId, toVersion, cancellationToken);
        if (oldSnapshotJson is null)
        {
            return Result.Failure<PublishResultDto>(
                Error.NotFound("Publishing.VersionNotFound", $"Kitap {bookId} için versiyon {toVersion} bulunamadı."));
        }

        var publishedAt = DateTime.UtcNow;

        await using var transaction = await _unitOfWork.BeginTransactionAsync(cancellationToken);

        var latestVersion = await _unitOfWork.Publications.GetLatestVersionAsync(bookId, cancellationToken);

        // Geriye gitme semantigi: hedef, mevcut en son versiyondan KESINLIKLE
        // eski olmali. Ayni versiyona "rollback" bos bir yayin uretir (anlamsiz,
        // yasak degil ama kafa karistirir); ileri versiyon zaten
        // GetSnapshotJsonAsync'te NotFound olarak elenir - burada asil amac
        // "mevcut = hedef" durumunu acikca reddetmek.
        if (toVersion >= latestVersion)
        {
            return Result.Failure<PublishResultDto>(Error.Validation(
                "Publishing.RollbackTargetNotOlder",
                $"Hedef versiyon ({toVersion}) mevcut en son versiyondan ({latestVersion}) eski olmalı."));
        }

        var newVersion = latestVersion + 1;
        book.Version = newVersion;

        // Eski snapshot'in ICINDEKI versiyon numarasi hala eski (toVersion) -
        // yeni bir yayin olarak yazildigi icin hem ust seviye Version hem
        // Book.Version alani yeni numarayla degistirilir. Modules/Contents
        // (dolayisiyla Blocks) OLDUGU GIBI kalir - geri alinan asil icerik bu.
        var oldSnapshot = SnapshotBuilder.Deserialize<SyncSnapshotDto>(oldSnapshotJson);
        var snapshot = oldSnapshot with
        {
            Version = newVersion,
            Book = oldSnapshot.Book with { Version = newVersion },
        };

        var publication = BuildPublicationShell(bookId, newVersion, snapshot, publishedAt, publishedById);

        var latestStates = await _unitOfWork.Publications
            .GetLatestContentStatesAsync(bookId, cancellationToken);

        AppendChangedContents(publication, snapshot, latestStates, bookId, newVersion);
        AppendTombstones(publication, snapshot, latestStates, bookId, newVersion);

        await _unitOfWork.Publications.AddAsync(publication, cancellationToken);

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            logger.LogInformation(ex, "Kitap {BookId} icin eszamanli rollback/publish yarisi - versiyon {Version} zaten yazilmis.", bookId, newVersion);
            return Result.Failure<PublishResultDto>(
                Error.Conflict(
                    "Publishing.VersionConflict",
                    "Aynı anda başka bir yayın yapıldı, lütfen tekrar deneyin."));
        }

        return Result.Success(new PublishResultDto(
            publication.Id,
            bookId,
            newVersion,
            snapshot.Contents.Count,
            publication.Checksum,
            publishedAt));
    }

    /// <summary>
    /// Snapshot'i bir kez serilestirip checksum'ini hesaplar, manifest'i uretir
    /// ve bos (henuz PublishedContents eklenmemis) BookPublication kabugunu doner.
    /// </summary>
    private static BookPublication BuildPublicationShell(
        int bookId, int newVersion, SyncSnapshotDto snapshot, DateTime publishedAt, string publishedById)
    {
        // Tek-serialize kurali: snapshot BIR KEZ serialize edilir; ayni baytlar
        // hem SnapshotJson kolonuna hem checksum'a gider. Baska hicbir yerde
        // yeniden serialize edilmez - invariant: Checksum = SHA256(SnapshotJson).
        var snapshotJson = SnapshotBuilder.Serialize(snapshot);
        var snapshotChecksum = SnapshotBuilder.ComputeChecksum(snapshotJson);

        // Manifest'in ContentCount'u "hayatta olan icerik sayisi" - tombstone'lar
        // snapshot.Contents'te olmadigi icin sayim bilerek boyle dogru.
        var manifest = SnapshotBuilder.BuildManifest(snapshot, publishedAt, snapshotChecksum);

        return new BookPublication
        {
            BookId = bookId,
            Version = newVersion,
            SnapshotJson = snapshotJson,
            ManifestJson = SnapshotBuilder.Serialize(manifest),
            Checksum = snapshotChecksum,
            PublishedAt = publishedAt,
            PublishedById = publishedById,
        };
    }

    /// <summary>Yeni veya son yayindan beri degisen (checksum farkli/dirilen) her content icin bir PublishedContent satiri ekler.</summary>
    private static void AppendChangedContents(
        BookPublication publication, SyncSnapshotDto snapshot, IReadOnlyList<PublishedContentState> latestStates, int bookId, int newVersion)
    {
        var stateByContentId = latestStates.ToDictionary(s => s.ContentId);

        foreach (var contentDto in snapshot.Contents)
        {
            // Bir kez serialize, ham metinden checksum - invariant:
            // Checksum = SHA256(PayloadJson), her satirda, tombstone dahil.
            var payload = SnapshotBuilder.Serialize(contentDto);
            var checksum = SnapshotBuilder.ComputeChecksum(payload);

            // Satir yalnizca icerik gercekten degistiyse yazilir: yeni content,
            // dirilis (son satir tombstone - checksum'i "{}"ninki oldugu icin
            // zaten farklidir, IsDeleted kontrolu niyeti acikca soyler) veya
            // checksum farki.
            if (stateByContentId.TryGetValue(contentDto.Id, out var state)
                && !state.IsDeleted
                && state.Checksum == checksum)
            {
                continue;
            }

            publication.PublishedContents.Add(new PublishedContent
            {
                BookId = bookId, // denormalize - bkz. PublishedContent.BookId yorumu
                ContentId = contentDto.Id,
                Version = newVersion,
                PayloadJson = payload,
                Checksum = checksum,
                IsDeleted = false,
            });
        }
    }

    /// <summary>Son durumu "hayatta" olup bu snapshot'ta artik olmayan her content icin bir kerelik tombstone satiri ekler.</summary>
    private static void AppendTombstones(
        BookPublication publication, SyncSnapshotDto snapshot, IReadOnlyList<PublishedContentState> latestStates, int bookId, int newVersion)
    {
        // Tombstone (6.4): son durumu "hayatta" olup bu snapshot'ta olmayan
        // content'ler. Bir kez yazilir - delta Version > from araligini
        // taradigi icin ondan eski her istemci tombstone'u er ya da gec gorur;
        // sonraki yayinlarda tekrarlamak sadece payload sisirirdi. Ilk
        // publish'te latestStates bos, dongu hic donmez.
        var currentIds = snapshot.Contents.Select(c => c.Id).ToHashSet();

        foreach (var state in latestStates.Where(s => !s.IsDeleted && !currentIds.Contains(s.ContentId)))
        {
            publication.PublishedContents.Add(new PublishedContent
            {
                BookId = bookId,
                ContentId = state.ContentId,
                Version = newVersion, // pazarliksiz: eski numarayla deltada kimse goremezdi
                PayloadJson = _tombstonePayload,
                Checksum = SnapshotBuilder.ComputeChecksum(_tombstonePayload),
                IsDeleted = true,
            });
        }
    }
}
