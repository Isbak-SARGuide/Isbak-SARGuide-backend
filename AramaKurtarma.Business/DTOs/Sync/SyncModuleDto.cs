namespace AramaKurtarma.Business.DTOs.Sync;

public sealed record SyncModuleDto(
    int Id,
    int BookId,
    string Name,
    string? Description,
    int DisplayOrder);
