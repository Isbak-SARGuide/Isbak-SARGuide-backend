namespace Isbak_SAR_Guide.Business.DTOs.Sync;

public sealed record MediaSummaryDto(
    int Id,
    string Url,
    string Checksum,
    long Size,
    // Faz 12.7, additive: bu ozellikten ONCE yuklenmis medyada null (backfill
    // yok) - istemci null'i "onizleme yok, Url'i kullan" olarak ele almali.
    string? ThumbnailUrl = null);
