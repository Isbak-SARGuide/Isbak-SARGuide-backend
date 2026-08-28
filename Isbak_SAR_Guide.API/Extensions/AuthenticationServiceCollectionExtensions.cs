using System.Text;
using Isbak_SAR_Guide.Business.Common;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace Microsoft.Extensions.DependencyInjection;

public static class AuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        // JwtOptions'in DI kaydi (IOptions<JwtOptions>, TokenService icin) ve
        // fail-fast dogrulamasi AddBusiness()'ta yapilir (StorageOptions'la ayni
        // desen, Faz 8 mimari incelemesinde bulundu: AddBusiness() tek basina
        // cagrilirsa - ornegin bu extension unutulursa - TokenService bos/varsayilan
        // bir anahtarla token imzalardi, baslangicta patlamak yerine). Burada
        // sadece JWT bearer semasini kurmak icin ayni bolum dogrudan okunur.
        var jwtOptions = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? throw new InvalidOperationException("'Jwt' konfigurasyon bolumu eksik.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });

        // GUVENLIK VARSAYILANI: hicbir attribute yazilmamis bir endpoint
        // KORUMALI sayilir. Acik olmasi gereken yerler [AllowAnonymous] ile
        // isaretlenir - "unutulan [Authorize]" riski boylece ortadan kalkar.
        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build();
        });

        return services;
    }
}
