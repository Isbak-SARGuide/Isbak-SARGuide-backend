using Isbak_SAR_Guide.Entities.Identity;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

/// <summary>
/// IRepository&lt;T&gt; genislemez: RefreshToken BaseEntity degil (bkz. entity'nin
/// kendi yorumu), generic CRUD sozlesmesi buraya uymuyor.
/// </summary>
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);

    Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reuse tespiti: iptal edilmis bir token tekrar sunulduysa calinmis
    /// olabilir - kullanicinin TUM aktif token'lari hemen iptal edilir.
    /// Degisiklik izleyiciyi (change tracker) atlar, dogrudan DB'ye yazar -
    /// bu bilerek ayri/acil bir islem, SaveChangesAsync'i beklemez.
    /// </summary>
    Task RevokeAllActiveForUserAsync(string userId, CancellationToken cancellationToken = default);
}
