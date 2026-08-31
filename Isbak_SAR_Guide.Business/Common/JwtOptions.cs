namespace Isbak_SAR_Guide.Business.Common;


public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public required string Issuer { get; init; }
    public required string Audience { get; init; }
    public required string SecretKey { get; init; }
    public int ExpiryMinutes { get; init; } = 60;

    public int RefreshTokenExpiryDays { get; init; } = 14;

    /// <summary>
    /// Eşzamanlı rotasyon yarışını (iki sekme/istek aynı refresh token'ı
    /// rotasyondan hemen önce/sonra sunması) hırsızlıktan ayırmak için - bu
    /// pencere içinde tekrar sunulan (zaten rotasyonla iptal edilmiş) bir token
    /// reuse-tespiti tetiklemez, normal bir yeni çift verilir. Pencere dışında
    /// sunulursa hâlâ hırsızlık şüphesiyle tüm token'lar iptal edilir - bkz.
    /// AuthService.RefreshAsync. roadmap doc §13.10.
    /// </summary>
    public int RefreshTokenRotationGraceSeconds { get; init; } = 10;
}

