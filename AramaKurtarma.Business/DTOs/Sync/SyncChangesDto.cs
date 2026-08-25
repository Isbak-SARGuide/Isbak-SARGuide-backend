namespace AramaKurtarma.Business.DTOs.Sync;

/// <summary>
/// STUB: gercek delta hesabi (BookPublication/PublishedContent versiyon
/// gecmisi) Faz 3/4'te gelecek. Su an her zaman "degisiklik yok" doner -
/// amac sema'yi mobil gelistirici icin simdiden dondurmak.
/// </summary>
public sealed record SyncChangesDto(
    int FromVersion,
    int ToVersion,
    IReadOnlyList<SyncContentDto> UpsertedContents,
    IReadOnlyList<int> DeletedContentIds,
    IReadOnlyList<MediaSummaryDto> AddedMedia,
    IReadOnlyList<int> RemovedMediaIds);
