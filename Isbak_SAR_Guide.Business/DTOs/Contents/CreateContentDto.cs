namespace Isbak_SAR_Guide.Business.DTOs.Contents;

/// <summary>
/// ModuleId route'tan gelir, DisplayOrder otomatik - bkz. CreateModuleDto.
/// IsPublished varsayilani TRUE - ayni gerekce CreateModuleDto'da.
/// </summary>
public sealed record CreateContentDto(
    string Title,
    string? Summary,
    bool IsPublished = true,
    string? VariantGroupKey = null,
    string? VariantLabel = null);
