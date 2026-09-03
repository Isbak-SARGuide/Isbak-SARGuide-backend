namespace Isbak_SAR_Guide.Business.DTOs.Publishing;

/// <summary>
/// PublishPreviewDto'nun eklenen/degisen/kaldirilan listelerindeki tek satir -
/// Module icin Name, Content icin Title buraya "Title" olarak akar (admin
/// ekraninda "hangi kayit" sorusuna cevap vermek icin id yeterli degil).
/// </summary>
public sealed record PublishPreviewItemDto(int Id, string Title);
