using AramaKurtarma.Business.Common;
using AramaKurtarma.Business.DTOs.Sync;

namespace AramaKurtarma.Business.Services.Abstract;

public interface ISyncService
{
    Task<Result<SyncManifestDto>> GetManifestAsync(int bookId, CancellationToken cancellationToken = default);

    Task<Result<SyncSnapshotDto>> GetSnapshotAsync(int bookId, CancellationToken cancellationToken = default);

    Task<Result<SyncChangesDto>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default);
}
