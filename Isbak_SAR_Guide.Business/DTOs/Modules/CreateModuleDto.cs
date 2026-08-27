namespace Isbak_SAR_Guide.Business.DTOs.Modules;

/// <summary>
/// BookId route'tan gelir, DisplayOrder servis tarafinda otomatik atanir -
/// client'in unique (BookId, DisplayOrder) constraint'ine carpma riski olmasin diye.
/// </summary>
public sealed record CreateModuleDto(
    string Name,
    string? Description,
    bool IsPublished = false);
