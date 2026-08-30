namespace Isbak_SAR_Guide.Business.DTOs.Modules;

public sealed record ModuleDto(
    int Id,
    int BookId,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    // Faz 13.4, additive: sadece GetPagedAsync doldurur (admin panel N+1
    // duzeltmesi) - GetByIdAsync/CreateAsync/UpdateAsync/ReorderAsync'te 0
    // kalir, tek bir modulu sayan ayri bir sorgu o yollarda gerekli degil.
    int ContentCount = 0);
