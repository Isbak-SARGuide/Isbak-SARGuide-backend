namespace Isbak_SAR_Guide.Business.DTOs.Books;

public sealed record UpdateBookDto(
    string Title,
    string Slug,
    string? Description,
    string LanguageCode);
