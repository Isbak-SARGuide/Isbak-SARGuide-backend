using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public sealed class TokenService(IOptions<JwtOptions> jwtOptions) : ITokenService
{
    private readonly JwtOptions _options = jwtOptions.Value;

    public AccessToken GenerateAccessToken(ApplicationUser user, IList<string> roles)
    {
        // Bitis zamani BIR KEZ hesaplanir; hem token'in icine hem de
        // donen kayda ayni deger gider.
        var expiresAtUtc = DateTime.UtcNow.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.UniqueName, user.UserName!),
            // Her token'a benzersiz kimlik - ileride token iptali (revocation)
            // gerekirse tek tek isaretleyebilmek icin.
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.FullName),
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: expiresAtUtc,
            signingCredentials: credentials);

        var encoded = new JwtSecurityTokenHandler().WriteToken(token);

        return new AccessToken(encoded, expiresAtUtc);
    }
}
