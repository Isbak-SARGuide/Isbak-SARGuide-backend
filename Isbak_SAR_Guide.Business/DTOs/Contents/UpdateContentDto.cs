namespace Isbak_SAR_Guide.Business.DTOs.Contents;

public sealed record UpdateContentDto(
    string Title,
    string? Summary,
    bool IsPublished,
    string? VariantGroupKey,
    string? VariantLabel);
