namespace Isbak_SAR_Guide.Business.DTOs.Books;

/// <summary>
/// Okuma modeli. Property adlari Book entity'siyle birebir ayni oldugu icin
/// Mapster'in convention-based eslesmesi ekstra config'e ihtiyac duymuyor -
/// serviste sadece book.Adapt&lt;BookDto&gt;() yeterli.
/// </summary>
public sealed record BookDto(
    int Id,
    string Title,
    string Slug,
    string? Description,
    string LanguageCode,
    int Version,
    bool IsPublished,
    DateTime CreatedAt,
    DateTime UpdatedAt);
