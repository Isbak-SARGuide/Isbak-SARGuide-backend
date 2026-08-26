namespace Isbak_SAR_Guide.Business.DTOs.Books;

public sealed record CreateBookDto(
    string Title,
    string Slug,
    string? Description,
    string LanguageCode = "tr");
