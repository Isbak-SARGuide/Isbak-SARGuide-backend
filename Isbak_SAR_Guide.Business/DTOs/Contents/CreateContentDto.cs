namespace Isbak_SAR_Guide.Business.DTOs.Contents;

/// <summary>ModuleId route'tan gelir, DisplayOrder otomatik - bkz. CreateModuleDto.</summary>
public sealed record CreateContentDto(
    string Title,
    string? Summary,
    bool IsPublished = false,
    string? VariantGroupKey = null,
    string? VariantLabel = null);
