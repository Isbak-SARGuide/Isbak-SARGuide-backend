namespace Isbak_SAR_Guide.Business.DTOs.Modules;

public sealed record ModuleDto(
    int Id,
    int BookId,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt);
