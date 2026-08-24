namespace AramaKurtarma.Business.Books;

public sealed record UpdateBookDto(
    string Title,
    string Slug,
    string? Description,
    string LanguageCode);
