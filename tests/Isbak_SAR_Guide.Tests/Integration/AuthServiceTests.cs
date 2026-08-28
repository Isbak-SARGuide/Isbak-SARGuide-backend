using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 9.1/9.3: refresh token rotasyonu, reuse tespiti ve lockout. Bilerek
/// paylasilan seed admin'i KULLANMAZ (lockout/reuse-revoke testleri o hesabi
/// gercekten kilitler/tum token'larini iptal eder - suitedeki diger onlarca
/// testin login için admin'e bagimli olmasi bunu tehlikeli yapar). Her test
/// kendi kullanicisini UserManager ile yaratir - CreateUserAsync yardimcisi.
/// </summary>
[Collection("Api")]
public class AuthServiceTests(ApiFactory factory)
{
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsAccessAndRefreshToken()
    {
        var (userName, password) = await CreateUserAsync();

        var result = await LoginAsync(userName, password);

        result.IsSuccess.ShouldBeTrue();
        result.Value.AccessToken.ShouldNotBeNullOrWhiteSpace();
        result.Value.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LoginAsync_AfterMaxFailedAttempts_LocksOutAccountEvenWithCorrectPassword()
    {
        var (userName, password) = await CreateUserAsync();

        // Lockout.MaxFailedAccessAttempts = 5 (DataAccess/DependencyInjection.cs).
        for (var i = 0; i < 5; i++)
        {
            (await LoginAsync(userName, "yanlis-sifre")).IsFailure.ShouldBeTrue();
        }

        // Dogru sifreyle bile - hesap artik kilitli. Jenerik mesaj (ayni
        // Unauthorized/Auth.InvalidCredentials) korunur, kilit varligini ifsa etmez.
        var result = await LoginAsync(userName, password);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Unauthorized);
        result.Error.Code.ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task RefreshAsync_WithValidToken_RotatesAndInvalidatesOldToken()
    {
        var (userName, password) = await CreateUserAsync();
        var login = await LoginAsync(userName, password);

        var refreshed = await RefreshAsync(login.Value.RefreshToken);
        refreshed.IsSuccess.ShouldBeTrue();
        refreshed.Value.RefreshToken.ShouldNotBe(login.Value.RefreshToken);

        // Eski token artik gecersiz - rotasyon tek kullanimlik olmali.
        var reuseOldToken = await RefreshAsync(login.Value.RefreshToken);
        reuseOldToken.IsFailure.ShouldBeTrue();
        reuseOldToken.Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task RefreshAsync_ReusingRevokedToken_RevokesAllActiveTokensForUser()
    {
        var (userName, password) = await CreateUserAsync();
        var login = await LoginAsync(userName, password);

        // Iki ayri "cihaz" gibi dusun: login'den gelen ilk refresh token
        // rotasyonla iptal edilsin, SONRA ikinci bir refresh ile YENI bir
        // aktif token uretilsin.
        var afterFirstRefresh = await RefreshAsync(login.Value.RefreshToken);
        afterFirstRefresh.IsSuccess.ShouldBeTrue();

        // Simdi ilk (zaten iptal edilmis) token TEKRAR sunuluyor - calinti sinyali.
        var reuseAttempt = await RefreshAsync(login.Value.RefreshToken);
        reuseAttempt.IsFailure.ShouldBeTrue();

        // Reuse tespiti, o andaki AKTIF token'i (ikinci refresh'ten geleni) da
        // iptal etmis olmali - artik o da kullanilamaz.
        var secondTokenAfterReuseDetected = await RefreshAsync(afterFirstRefresh.Value.RefreshToken);
        secondTokenAfterReuseDetected.IsFailure.ShouldBeTrue();
        secondTokenAfterReuseDetected.Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task RefreshAsync_WithUnknownToken_ReturnsUnauthorized()
    {
        var result = await RefreshAsync("hicbir-zaman-uretilmemis-bir-token");

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredToken_ReturnsUnauthorized()
    {
        var (userName, password) = await CreateUserAsync();
        var login = await LoginAsync(userName, password);

        await BackdateRefreshTokenExpiryAsync(login.Value.RefreshToken);

        var result = await RefreshAsync(login.Value.RefreshToken);

        result.IsFailure.ShouldBeTrue();
        result.Error!.Type.ShouldBe(ErrorType.Unauthorized);
    }

    [Fact]
    public async Task RevokeAsync_ValidToken_PreventsFurtherRefresh()
    {
        var (userName, password) = await CreateUserAsync();
        var login = await LoginAsync(userName, password);

        var revokeResult = await RevokeAsync(login.Value.RefreshToken);
        revokeResult.IsSuccess.ShouldBeTrue();

        (await RefreshAsync(login.Value.RefreshToken)).IsFailure.ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeAsync_CalledTwice_IsIdempotent()
    {
        var (userName, password) = await CreateUserAsync();
        var login = await LoginAsync(userName, password);

        (await RevokeAsync(login.Value.RefreshToken)).IsSuccess.ShouldBeTrue();
        (await RevokeAsync(login.Value.RefreshToken)).IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeAsync_UnknownToken_ReturnsSuccessIdempotently()
    {
        var result = await RevokeAsync("hicbir-zaman-uretilmemis-bir-token");

        result.IsSuccess.ShouldBeTrue();
    }

    // ---- Yardımcılar ----

    private async Task<Result<LoginResponseDto>> LoginAsync(string userName, string password)
    {
        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        return await authService.LoginAsync(new LoginDto(userName, password));
    }

    private async Task<Result<LoginResponseDto>> RefreshAsync(string refreshToken)
    {
        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        return await authService.RefreshAsync(new RefreshTokenRequestDto(refreshToken));
    }

    private async Task<Result> RevokeAsync(string refreshToken)
    {
        using var scope = factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        return await authService.RevokeAsync(new RefreshTokenRequestDto(refreshToken));
    }

    private async Task<(string UserName, string Password)> CreateUserAsync()
    {
        var userName = $"auth-test-{Guid.NewGuid():N}";
        const string password = "AuthTest!2026x";

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@isbak-sar-guide.local",
            EmailConfirmed = true,
            FullName = "Auth Test Kullanıcısı",
        };

        var createResult = await userManager.CreateAsync(user, password);
        createResult.Succeeded.ShouldBeTrue(string.Join("; ", createResult.Errors.Select(e => e.Description)));

        return (userName, password);
    }

    private async Task BackdateRefreshTokenExpiryAsync(string rawRefreshToken)
    {
        using var scope = factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var dbContext = scope.ServiceProvider.GetRequiredService<Isbak_SAR_GuideDbContext>();

        var tokenHash = tokenService.HashRefreshToken(rawRefreshToken);
        var token = await dbContext.RefreshTokens.SingleAsync(t => t.TokenHash == tokenHash);
        token.ExpiresAtUtc = DateTime.UtcNow.AddDays(-1);
        await dbContext.SaveChangesAsync();
    }
}
