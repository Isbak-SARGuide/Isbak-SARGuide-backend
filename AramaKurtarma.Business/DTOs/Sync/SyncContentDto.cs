namespace AramaKurtarma.Business.DTOs.Sync;

public sealed record SyncContentDto(
    int Id,
    int ModuleId,
    string Title,
    string? Summary,
    int DisplayOrder,
    IReadOnlyList<SyncContentBlockDto> Blocks);
