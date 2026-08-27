using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Faz 5 CMS controller'lari icin routing/DI/auth wiring smoke testleri -
/// AuthTests.cs'teki GetBooks_WithValidToken_ReturnsOk deseniyle ayni. Servis
/// davranisinin derinlemesine testi ModuleServiceTests/ContentServiceTests/
/// ContentBlockServiceTests'te; burada sadece HTTP katmaninin gercekten
/// bagli oldugu kanitlanir. Paylasilan seed kitabi (id 1) sadece okuma icin
/// kullanilir.
/// </summary>
[Collection("Api")]
public class CmsControllerSmokeTests(ApiFactory factory)
{
    [Fact]
    public async Task GetModules_WithValidToken_ReturnsOk()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/books/1/modules");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetModules_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/books/1/modules");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetContents_ForNonExistentModule_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/modules/999999/contents");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBlocks_ForNonExistentContent_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/contents/999999/blocks");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient();

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "Admin!Dev123" });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        return client;
    }
}
