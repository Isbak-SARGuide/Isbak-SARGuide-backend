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

        // STUB: gercek PublishedAt, Faz 4'te BookPublication.PublishedAt'ten okunacak.
        var manifest = SnapshotBuilder.BuildManifest(snapshotResult.Value, DateTime.UtcNow);

        return Result.Success(manifest);
    }

    public Task<Result<SyncChangesDto>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default)
    {
        // STUB: gercek delta hesabi PublishedContent.Version uzerinden Faz 4'te gelecek.
        var changes = new SyncChangesDto(fromVersion, fromVersion, [], [], [], []);
        return Task.FromResult(Result.Success(changes));
    }
}
