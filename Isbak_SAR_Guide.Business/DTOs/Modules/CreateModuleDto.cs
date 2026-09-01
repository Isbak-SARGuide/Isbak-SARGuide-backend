namespace Isbak_SAR_Guide.Business.DTOs.Modules;

/// <summary>
/// BookId route'tan gelir, DisplayOrder servis tarafinda otomatik atanir -
/// client'in unique (BookId, DisplayOrder) constraint'ine carpma riski olmasin diye.
/// IsPublished varsayilani TRUE (Backend-Yapilacaklar.md #3): eskiden false'tu,
/// yani editor bir modul olusturup hemen kitabi yayinlarsa (13.3'un IsPublished
/// filtresi yuzunden) icerik sessizce disarida kaliyordu - "opt-in publish"
/// yerine "opt-out draft" modeli tercih edildi, surpriz daha az.
/// </summary>
public sealed record CreateModuleDto(
    string Name,
    string? Description,
    bool IsPublished = true);
