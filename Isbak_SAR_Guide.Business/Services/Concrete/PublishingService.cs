using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Publishing;
using Isbak_SAR_Guide.Business.Mapping;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Common;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class PublishingService(IUnitOfWork unitOfWork) : IPublishingService
{
    /// <summary>
    /// Tombstone satirinin payload'i: icerik artik yok, kimligi ContentId
    /// kolonu tasiyor - son bilinen payload'i tasimak hem bayt israfi hem
    /// anlam bozuklugu olurdu. Bos obje, jsonb NOT NULL kisitini da karsilar.
    /// </summary>
    private const string TombstonePayload = "{}";

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

        // Manifest'in ContentCount'u "hayatta olan icerik sayisi" - tombstone'lar
        // snapshot.Contents'te olmadigi icin sayim bilerek boyle dogru.
        var manifest = SnapshotBuilder.BuildManifest(snapshot, publishedAt);

        var publication = new BookPublication
        {
            BookId = bookId,
            Version = newVersion,
            ManifestJson = SnapshotBuilder.Serialize(manifest),
            Checksum = manifest.Checksum,
            PublishedAt = publishedAt,
            PublishedById = publishedById,
        };

        foreach (var contentDto in snapshot.Contents)
        {
            // Bir kez serialize, ham metinden checksum - invariant:
            // Checksum = SHA256(PayloadJson), her satirda, tombstone dahil.
            var payload = SnapshotBuilder.Serialize(contentDto);

            publication.PublishedContents.Add(new PublishedContent
            {
                BookId = bookId, // denormalize - bkz. PublishedContent.BookId yorumu
                ContentId = contentDto.Id,
                Version = newVersion,
                PayloadJson = payload,
                Checksum = SnapshotBuilder.ComputeChecksum(payload),
                IsDeleted = false,
            });
        }

        // Tombstone (6.4): onceki yayinda hayatta olup bu snapshot'ta olmayan
        // content'ler. Bir kez yazilir - delta Version > from araligini
        // taradigi icin ondan eski her istemci tombstone'u er ya da gec gorur;
        // sonraki yayinlarda tekrarlamak sadece payload sisirirdi. Ilk
        // publish'te latestVersion=0 hic satir bulmaz, dongu hic donmez.
        var currentIds = snapshot.Contents.Select(c => c.Id).ToHashSet();
        var previousAlive = await _unitOfWork.Publications
            .GetActiveContentIdsAsync(bookId, latestVersion, cancellationToken);

        foreach (var deletedId in previousAlive.Where(id => !currentIds.Contains(id)))
        {
            publication.PublishedContents.Add(new PublishedContent
            {
                BookId = bookId,
                ContentId = deletedId,
                Version = newVersion, // pazarliksiz: eski numarayla deltada kimse goremezdi
                PayloadJson = TombstonePayload,
                Checksum = SnapshotBuilder.ComputeChecksum(TombstonePayload),
                IsDeleted = true,
            });
        }

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
}
