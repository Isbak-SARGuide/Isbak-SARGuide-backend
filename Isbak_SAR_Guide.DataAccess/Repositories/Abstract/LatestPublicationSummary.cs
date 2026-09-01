namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

/// <summary>
/// PublishingService.PublishAsync'in "hicbir sey degismedi, no-op don"
/// karari icin dar bir projeksiyon - son yayinin no-op karsilastirmasi
/// (Checksum) ve geri donusu (Id, Version, PublishedAt) icin yeterli
/// alanlar, SnapshotJson/ManifestJson (megabaytlik kolonlar) HIC cekilmez.
/// </summary>
public sealed record LatestPublicationSummary(int Id, int Version, string Checksum, DateTime PublishedAt);
