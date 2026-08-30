namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

/// <summary>
/// GetHistoryAsync'in bir yayin icin dondurdugu ham satir - SnapshotJson
/// (megabaytlik kolon) HIC cekilmez. PublishedAt/ContentCount/Checksum
/// ManifestJson'un icinde zaten var (SyncManifestDto ile ayni alanlar) -
/// Business katmani bunlari tek bir deserialize ile cikarir, burada ayrica
/// kolon olarak tasinmalari gereksiz tekrar olurdu.
/// </summary>
public sealed record PublicationHistoryRow(int PublicationId, int Version, string PublishedByUserName, string ManifestJson);
