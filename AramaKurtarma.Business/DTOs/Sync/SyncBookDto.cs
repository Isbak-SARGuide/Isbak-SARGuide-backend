namespace AramaKurtarma.Business.DTOs.Sync;

public sealed record SyncBookDto(
    int Id,
    string Title,
    string Slug,
    string? Description,
    string LanguageCode,
    int Version);
