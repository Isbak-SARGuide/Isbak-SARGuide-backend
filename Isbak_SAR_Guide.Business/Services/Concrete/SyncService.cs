using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Sync;
using Isbak_SAR_Guide.Business.Mapping;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

/// <summary>
/// STUB (5.0): draft veriden calisir, gercek yayin/versiyon sistemi (Faz 3/4)
/// gelmeden mobil gelistiricinin sozlesme uzerinde ilerleyebilmesi icin.
/// GetChangesAsync bu yuzden her zaman "degisiklik yok" doner.
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

    public async Task<Result<SyncManifestDto>> GetManifestAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var snapshotResult = await GetSnapshotAsync(bookId, cancellationToken);

        if (snapshotResult.IsFailure)
        {
            return Result.Failure<SyncManifestDto>(snapshotResult.Error!);
        }

        var snapshot = snapshotResult.Value;

        var media = snapshot.Contents
            .SelectMany(c => c.Blocks)
            .Select(b => b.Media)
            .OfType<MediaSummaryDto>()
            .DistinctBy(m => m.Id)
            .ToList();

        var manifest = new SyncManifestDto(
            snapshot.Book.Id,
            snapshot.Version,
            DateTime.UtcNow, // Gercek PublishedAt Faz 3'te (BookPublication.PublishedAt) gelecek
            snapshot.Contents.Count,
            media,
            SnapshotBuilder.ComputeChecksum(snapshot));

        return Result.Success(manifest);
    }

    public Task<Result<SyncChangesDto>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default)
    {
        // STUB: gercek delta hesabi PublishedContent.Version uzerinden Faz 4'te gelecek.
        var changes = new SyncChangesDto(fromVersion, fromVersion, [], [], [], []);
        return Task.FromResult(Result.Success(changes));
    }
}
