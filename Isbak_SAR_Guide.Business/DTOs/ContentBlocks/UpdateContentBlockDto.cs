using Isbak_SAR_Guide.Entities.Content.Enums;

namespace Isbak_SAR_Guide.Business.DTOs.ContentBlocks;

public sealed record UpdateContentBlockDto(
    ContentBlockType Type,
    string? Text,
    string? DataJson,
    int? MediaId);
