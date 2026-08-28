using Isbak_SAR_Guide.Entities.Content.Enums;

namespace Isbak_SAR_Guide.Business.DTOs.ContentBlocks;

/// <summary>ContentId route'tan gelir, DisplayOrder otomatik - bkz. CreateModuleDto.</summary>
public sealed record CreateContentBlockDto(
    ContentBlockType Type,
    string? Text,
    string? DataJson,
    int? MediaId);
