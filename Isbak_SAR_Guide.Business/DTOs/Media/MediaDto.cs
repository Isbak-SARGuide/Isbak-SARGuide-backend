using Isbak_SAR_Guide.Entities.Content.Enums;

namespace Isbak_SAR_Guide.Business.DTOs.Media;

public sealed record MediaDto(
    int Id,
    string FileName,
    string StoragePath,
    MediaType MediaType,
    string ContentType,
    long FileSize,
    string Checksum,
    int? Width,
    int? Height,
    double? Duration,
    DateTime CreatedAt,
    // Faz 12.7, additive: null = bu medyanin thumbnail'i yok (backfill yok,
    // sadece bu ozellikten sonraki yuklemeler doldurur).
    string? ThumbnailStoragePath = null);
