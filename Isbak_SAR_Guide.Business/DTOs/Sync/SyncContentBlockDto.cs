using Isbak_SAR_Guide.Entities.Content.Enums;

namespace Isbak_SAR_Guide.Business.DTOs.Sync;

public sealed record SyncContentBlockDto(
    int Id,
    ContentBlockType Type,
    string? Text,
    string? DataJson,
    MediaSummaryDto? Media,
    int DisplayOrder);
