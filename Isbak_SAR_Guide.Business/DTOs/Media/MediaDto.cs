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
    DateTime CreatedAt);
