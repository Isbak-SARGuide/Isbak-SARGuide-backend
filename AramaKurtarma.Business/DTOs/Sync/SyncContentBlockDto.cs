using AramaKurtarma.Entities.Content.Enums;

namespace AramaKurtarma.Business.DTOs.Sync;

public sealed record SyncContentBlockDto(
    int Id,
    ContentBlockType Type,
    string? Text,
    string? DataJson,
    MediaSummaryDto? Media,
    int DisplayOrder);
