namespace AramaKurtarma.Business.DTOs.Books;

public sealed record UpdateBookDto(
    string Title,
    string Slug,
    string? Description,
    string LanguageCode);
