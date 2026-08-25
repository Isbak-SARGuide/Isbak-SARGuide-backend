using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Sync;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

public interface ISyncService
{
    Task<Result<SyncManifestDto>> GetManifestAsync(int bookId, CancellationToken cancellationToken = default);

    Task<Result<SyncSnapshotDto>> GetSnapshotAsync(int bookId, CancellationToken cancellationToken = default);

    Task<Result<SyncChangesDto>> GetChangesAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default);
}
