using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Business.DTOs.Media;
using Isbak_SAR_Guide.Entities.Identity;
using Isbak_SAR_Guide.Tests.Unit;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// MediaController'in HTTP katmani - MediaServiceTests.cs servis davranisini
/// zaten derinlemesine kapsiyor, burasi routing/model-binding/auth wiring'in
/// gercekten calistigini kanitlar (AuthTests.cs/CmsControllerSmokeTests.cs
/// deseniyle ayni gerekce). cleanup-orphans'in Admin-only kisitini gercek bir
/// Editor kullaniciyla (403) ve gercek admin ile (200) test eder.
/// </summary>
[Collection("Api")]
public class MediaControllerTests(ApiFactory factory)
{
    [Fact]
    public async Task Upload_WithValidTokenAndPng_ReturnsOkWithMediaDto()
    {
        var client = await CreateAuthenticatedClientAsync();
        using var content = BuildMultipartPng(TestImageFactory.BuildRealPng(3, 4), "foto.png");

        var response = await client.PostAsync("/api/v1/media", content);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var media = await response.Content.ReadFromJsonAsync<MediaDto>();
        // Faz 12.7: STORAGE'A YAZILAN dosya artik her zaman WebP.
        media!.ContentType.ShouldBe("image/webp");
        media.Width.ShouldBe(3);
        media.Height.ShouldBe(4);
    }

    [Fact]
    public async Task Upload_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        using var content = BuildMultipartPng(TestImageFactory.BuildRealPng(1, 1), "foto.png");

        var response = await client.PostAsync("/api/v1/media", content);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Upload_WithoutFilePart_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        using var content = new MultipartFormDataContent();

        var response = await client.PostAsync("/api/v1/media", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_WithNonImageBytes_ReturnsBadRequest()
    {
        var client = await CreateAuthenticatedClientAsync();
        using var content = BuildMultipartPng([0x01, 0x02, 0x03], "sahte.png");

        var response = await client.PostAsync("/api/v1/media", content);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetById_AfterUpload_ReturnsOk()
    {
        var client = await CreateAuthenticatedClientAsync();
        var uploaded = await UploadAsync(client, TestImageFactory.BuildRealPng(5, 5), "get-test.png");

        var response = await client.GetAsync($"/api/v1/media/{uploaded.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetById_NonExistent_ReturnsNotFound()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/media/999999");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_UnreferencedMedia_ReturnsNoContent()
    {
        var client = await CreateAuthenticatedClientAsync();
        var uploaded = await UploadAsync(client, TestImageFactory.BuildRealPng(6, 6), "delete-test.png");

        var response = await client.DeleteAsync($"/api/v1/media/{uploaded.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CleanupOrphans_WithAdminToken_ReturnsOk()
    {
        var client = await CreateAuthenticatedClientAsync();

        var response = await client.PostAsync("/api/v1/media/cleanup-orphans", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CleanupOrphans_WithEditorToken_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedEditorClientAsync();

        var response = await client.PostAsync("/api/v1/media/cleanup-orphans", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CleanupOrphans_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/v1/media/cleanup-orphans", content: null);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Yardımcılar ----

    private static MultipartFormDataContent BuildMultipartPng(byte[] bytes, string fileName)
    {
        var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(bytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(filePart, "file", fileName);
        return content;
    }

    private async Task<MediaDto> UploadAsync(HttpClient client, byte[] bytes, string fileName)
    {
        using var content = BuildMultipartPng(bytes, fileName);
        var response = await client.PostAsync("/api/v1/media", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<MediaDto>())!;
    }

    private async Task<HttpClient> CreateAuthenticatedClientAsync()
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "Admin!Dev123" });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        return client;
    }

    /// <summary>
    /// Rol kisitini (cleanup-orphans Admin-only) gercekten dogrulamanin tek
    /// yolu gercek bir Editor kullanicisiyla giris yapmak - seed sadece Admin
    /// uretiyor, bu yuzden test kendi Editor'unu Identity API'siyle olusturur.
    /// </summary>
    private async Task<HttpClient> CreateAuthenticatedEditorClientAsync()
    {
        const string userName = "editor-test-user";
        const string password = "Editor!Test123";

        using (var scope = factory.Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (await userManager.FindByNameAsync(userName) is null)
            {
                var editor = new ApplicationUser
                {
                    UserName = userName,
                    Email = $"{userName}@isbak-sar-guide.local",
                    EmailConfirmed = true,
                    FullName = "Test Editörü",
                };

                var createResult = await userManager.CreateAsync(editor, password);
                createResult.Succeeded.ShouldBeTrue(string.Join("; ", createResult.Errors.Select(e => e.Description)));
                await userManager.AddToRoleAsync(editor, RoleNames.Editor);
            }
        }

        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        return client;
    }
}
