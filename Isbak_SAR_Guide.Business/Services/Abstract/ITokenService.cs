using Isbak_SAR_Guide.Entities.Identity;

namespace Isbak_SAR_Guide.Business.Services.Abstract;

/// <summary>
/// Uretilen token ve gecerlilik bitisi birlikte doner. Bitis zamanini
/// cagiran tarafin yeniden hesaplamasi iki kaynak yaratir - token 60 dk
/// derken cevap 30 dk derse istemci yanlis anda yeniler.
/// </summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

/// <summary>Ham token - sadece istemciye BiR KEZ donulur, DB'de hic saklanmaz (bkz. HashRefreshToken).</summary>
public sealed record RefreshTokenResult(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken GenerateAccessToken(ApplicationUser user, IList<string> roles);

    RefreshTokenResult GenerateRefreshToken();

    /// <summary>SHA-256 hex ozeti - sifre gibi, DB'de hep bu saklanir, ham token asla.</summary>
    string HashRefreshToken(string token);
}
