namespace AramaKurtarma.Business.Books;

public sealed record CreateBookDto(
    string Title,
    string Slug,
    string? Description,
    string LanguageCode = "tr");
