using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Concrete;

public class RefreshTokenRepository(Isbak_SAR_GuideDbContext dbContext) : IRefreshTokenRepository
{
    private readonly DbSet<RefreshToken> _tokens = dbContext.Set<RefreshToken>();

    public async Task<RefreshToken?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) =>
        await _tokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task AddAsync(RefreshToken token, CancellationToken cancellationToken = default) =>
        await _tokens.AddAsync(token, cancellationToken);

    public async Task RevokeAllActiveForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await _tokens
            .Where(t => t.UserId == userId && t.RevokedAtUtc == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(t => t.RevokedAtUtc, now), cancellationToken);
    }
}
