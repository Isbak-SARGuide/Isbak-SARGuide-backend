namespace Isbak_SAR_Guide.Business.DTOs.Modules;

public sealed record UpdateModuleDto(
    string Name,
    string? Description,
    bool IsPublished);
