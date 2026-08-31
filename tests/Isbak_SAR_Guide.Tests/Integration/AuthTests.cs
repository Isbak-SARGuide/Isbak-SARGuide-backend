using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

[Collection("Api")]
public class AuthTests(ApiFactory factory)
{
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsOkWithAccessToken()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "Admin!Dev123" });

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "Admin!Dev12i3" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetBooks_WithValidToken_ReturnsOk()
    {
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "Admin!Dev123" });

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        var response = await client.GetAsync("api/v1/books");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

    }

    [Fact]
    public async Task Refresh_WithValidRefreshToken_ReturnsNewTokenPair()
    {
        // Admin degil, kendi kullanicisi - suitedeki digerlerini
        // (admin refresh token'lariyla ilgisi olmayan) etkilemesin.
        var (userName, password) = await CreateTestUserAsync();
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = loginResult!.RefreshToken });
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var refreshResult = await refreshResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        refreshResult!.RefreshToken.ShouldNotBe(loginResult.RefreshToken);

        // Rotasyon grace window'u (roadmap doc §13.10) icinde ayni token'in
        // HEMEN tekrar sunulmasi artik reddedilmiyor - esizamanli bir yaris
        // olabilecegi icin yeni bir cift verilir, tum token'lar iptal edilmez.
        // Bkz. AuthServiceTests.cs'teki grace-window/reuse testleri, ayrintili
        // senaryolar orada.
        var racingRetryResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = loginResult.RefreshToken });
        racingRetryResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Revoke_WithValidRefreshToken_InvalidatesIt()
    {
        var (userName, password) = await CreateTestUserAsync();
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

        var revokeResponse = await client.PostAsJsonAsync("/api/v1/auth/revoke", new { refreshToken = loginResult!.RefreshToken });
        revokeResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = loginResult.RefreshToken });
        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<(string UserName, string Password)> CreateTestUserAsync()
    {
        var userName = $"auth-http-test-{Guid.NewGuid():N}";
        const string password = "AuthTest!2026x";

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@isbak-sar-guide.local",
            EmailConfirmed = true,
            FullName = "Auth HTTP Test Kullanıcısı",
        };

        await userManager.CreateAsync(user, password);
        return (userName, password);
    }
}
