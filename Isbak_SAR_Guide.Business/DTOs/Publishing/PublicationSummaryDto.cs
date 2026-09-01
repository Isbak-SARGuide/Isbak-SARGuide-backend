namespace Isbak_SAR_Guide.Business.DTOs.Publishing;

/// <summary>
/// Yayin gecmisi listesindeki tek satir - rollback UI'inin "hangi versiyona
/// donulebilir" dropdown'i icin (web ekibinin geri bildirimi, bkz.
/// Frontend-Notlar-ve-Oneriler.md madde 9b). SnapshotJson bilerek TASINMAZ -
/// PublishResultDto'nun ayni gerekcesi (entity/megabaytlik kolon sizdirmamak).
/// </summary>
public sealed record PublicationSummaryDto(
    int PublicationId,
    int Version,
    DateTime PublishedAt,
    string PublishedByUserName,
    int ContentCount,
    string Checksum);
