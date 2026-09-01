namespace Isbak_SAR_Guide.Business.Common;

/// <summary>
/// Faz 12.8: TUM istekler icin IP basina sabit-pencere limiti (login/refresh'in
/// kendi, cok daha siki "login" politikasindan bagimsiz, ek bir katman).
/// En buyuk risk /sync/* uclari - AllowAnonymous, kimlik dogrulamasi yok - ama
/// global limiter tanimi geregi TUM endpoint'leri kapsar. Ayri bir IOptions tipi
/// (LoginRateLimitOptions'la ayni gerekce): request-zamanli PostConfigure ile
/// ezilebilsin, ApiFactory paylasilan TestServer'da tum testleri tek partition'a
/// dusurmesin (RemoteIpAddress in-memory host'ta null'a duser).
/// </summary>
public sealed class GlobalRateLimitOptions
{
    public const string SectionName = "RateLimiting:Global";

    public int PermitLimit { get; set; } = 300;

    public int WindowSeconds { get; set; } = 60;
}
