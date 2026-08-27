namespace Isbak_SAR_Guide.Business.Common;

/// <summary>
/// Faz 9.3: login/refresh icin IP basina sabit-pencere limiti. Ayri bir
/// IOptions tipi olarak tutulur (JwtOptions'a gomulmez) ki request-zamanli
/// PostConfigure ile ezilebilsin - bkz. tests/.../ApiFactory.cs: paylasilan
/// WebApplicationFactory'nin in-memory TestServer'inda RemoteIpAddress hep
/// null'a dustugu icin TUM testler ayni partition'i paylasir, gercek deger
/// (5/dk) suitedeki diger testleri 429'a dusururdu.
/// </summary>
public sealed class LoginRateLimitOptions
{
    public const string SectionName = "RateLimiting:Login";

    public int PermitLimit { get; set; } = 5;

    public int WindowSeconds { get; set; } = 60;
}
