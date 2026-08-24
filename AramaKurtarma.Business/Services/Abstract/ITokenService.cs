using AramaKurtarma.Entities.Identity;

namespace AramaKurtarma.Business.Services.Abstract;

/// <summary>
/// Uretilen token ve gecerlilik bitisi birlikte doner. Bitis zamanini
/// cagiran tarafin yeniden hesaplamasi iki kaynak yaratir - token 60 dk
/// derken cevap 30 dk derse istemci yanlis anda yeniler.
/// </summary>
public sealed record AccessToken(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    AccessToken GenerateAccessToken(ApplicationUser user, IList<string> roles);
}
