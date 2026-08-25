namespace Isbak_SAR_Guide.Business.DTOs.Sync;

/// <summary>
/// Mobil her acilista bunu ceker (kucuk payload). Sunucu versiyonu ile
/// yerel versiyon farkli mi diye bakmak icin - farkliysa changes/snapshot cekilir.
/// </summary>
public sealed record SyncManifestDto(
    int BookId,
    int Version,
    DateTime PublishedAt,
    int ContentCount,
    IReadOnlyList<MediaSummaryDto> Media,
    string Checksum);
