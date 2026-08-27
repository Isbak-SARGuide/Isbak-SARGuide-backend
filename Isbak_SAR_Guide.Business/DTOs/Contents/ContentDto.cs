namespace Isbak_SAR_Guide.Business.DTOs.Contents;

public sealed record ContentDto(
    int Id,
    int ModuleId,
    string Title,
    string? Summary,
    int DisplayOrder,
    bool IsPublished,
    string? VariantGroupKey,
    string? VariantLabel,
    DateTime CreatedAt,
    DateTime UpdatedAt);
