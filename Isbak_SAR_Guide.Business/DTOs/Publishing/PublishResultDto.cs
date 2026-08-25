namespace Isbak_SAR_Guide.Business.DTOs.Publishing;

/// <summary>
/// Publish isleminin kaniti; admin dashboard'a doner. Entity (BookPublication)
/// bilerek donulmez - navigation'lari ve ManifestJson'i sizdirmamak icin.
/// PublicationId, POST ile yaratilan kaynagin kimligi (ileride yayin gecmisi
/// ekrani bu id ile detaya iner).
/// </summary>
public sealed record PublishResultDto(
    int PublicationId,
    int BookId,
    int Version,
    int ContentCount,
    string Checksum,
    DateTime PublishedAt);
