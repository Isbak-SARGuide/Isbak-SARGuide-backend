using Isbak_SAR_Guide.Entities.Content.Enums;

namespace Isbak_SAR_Guide.Business.DTOs.ContentBlocks;

public sealed record ContentBlockDto(
    int Id,
    int ContentId,
    ContentBlockType Type,
    string? Text,
    string? DataJson,
    int? MediaId,
    int DisplayOrder,
    DateTime CreatedAt,
    DateTime UpdatedAt);
