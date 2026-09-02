using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>Faz 9.2: POST /api/v1/users - Admin-only, gercek bir Editor kullaniciyla (403) dogrulanir.</summary>
[Collection("Api")]
public class UsersControllerTests(ApiFactory factory)
{
    [Fact]
    public async Task Create_WithAdminToken_ReturnsOk()
    {
        var client = await CreateAuthenticatedAdminClientAsync();
        var newUser = new { userName = $"created-by-admin-{Guid.NewGuid():N}", password = "Editor!2026Pass", fullName = "Yeni Kullanıcı", role = RoleNames.Editor };

        var response = await client.PostAsJsonAsync("/api/v1/users", newUser);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_WithEditorToken_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedEditorClientAsync();
        var newUser = new { userName = $"should-not-be-created-{Guid.NewGuid():N}", password = "Editor!2026Pass", fullName = "X", role = RoleNames.Editor };

        var response = await client.PostAsJsonAsync("/api/v1/users", newUser);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();
        var newUser = new { userName = "kimliksiz", password = "Editor!2026Pass", fullName = "X", role = RoleNames.Editor };

        var response = await client.PostAsJsonAsync("/api/v1/users", newUser);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetAll_WithEditorToken_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedEditorClientAsync();

        var response = await client.GetAsync("/api/v1/users?page=1&pageSize=10");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAll_WithAdminToken_ReturnsOk()
    {
        var client = await CreateAuthenticatedAdminClientAsync();

        var response = await client.GetAsync("/api/v1/users?page=1&pageSize=10");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetAll_OversizedPageSize_IsClampedToMax()
    {
        // Backend-Yapilacaklar.md #2: pageSize=100000 gibi bir deger sunucuyu
        // gereksiz buyuk bir sorguya zorlamamali - kirpilmis (200) etkin
        // pageSize, PagedResult zarfinda geri yansimali.
        var client = await CreateAuthenticatedAdminClientAsync();

        var response = await client.GetAsync("/api/v1/users?page=1&pageSize=100000");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        body.RootElement.GetProperty("pageSize").GetInt32().ShouldBe(200);
    }

    [Fact]
    public async Task ChangeRole_WithEditorToken_ReturnsForbidden()
    {
        // Rol degistirme, ayricalik yukseltme (privilege escalation) riski
        // tasiyan en hassas eylem - [Authorize(Roles = RoleNames.Admin)]
        // yanlislikla dususe bu test yakalamali (kod inceleme bulgusu:
        // ChangeRole bu kapsamayan tek Admin-only eylemdi).
        var client = await CreateAuthenticatedEditorClientAsync();

        var response = await client.PutAsJsonAsync($"/api/v1/users/{Guid.NewGuid()}/role", new { role = RoleNames.Admin });

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_WithEditorToken_ReturnsForbidden()
    {
        var client = await CreateAuthenticatedEditorClientAsync();

        var response = await client.DeleteAsync($"/api/v1/users/{Guid.NewGuid()}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_WithAdminToken_RemovesEditorUser()
    {
        var adminClient = await CreateAuthenticatedAdminClientAsync();
        var newUser = new { userName = $"delete-http-{Guid.NewGuid():N}", password = "Editor!2026Pass", fullName = "HTTP Silme", role = RoleNames.Editor };
        var createResponse = await adminClient.PostAsJsonAsync("/api/v1/users", newUser);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("id").GetString();

        var deleteResponse = await adminClient.DeleteAsync($"/api/v1/users/{userId}");

        deleteResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var loginResponse = await factory.CreateClient().PostAsJsonAsync("/api/v1/auth/login", new { userName = newUser.userName, password = newUser.password });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Delete_WithAdminTokenTargetingAdminUser_ReturnsBadRequest()
    {
        var adminClient = await CreateAuthenticatedAdminClientAsync();

        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByNameAsync("admin");

        var response = await adminClient.DeleteAsync($"/api/v1/users/{admin!.Id}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithEditorToken_ReturnsNoContent()
    {
        // 13.6'nin en onemli auth-wiring kaniti: sinif seviyesindeki
        // [Authorize(Roles = RoleNames.Admin)] burada eylem-seviyesindeki
        // [Authorize] tarafindan GECERSIZ KILINMALI - bir Editor (Admin
        // DEGIL) kendi sifresini degistirebilmeli, 403 almamali.
        const string password = "Editor!Test123";
        var client = await CreateAuthenticatedEditorClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/password", new { currentPassword = password, newPassword = "Editor!Test456" });

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Sifreyi eski haline geri al - bu client sabit bir test kullanicisi
        // kullaniyor (CreateAuthenticatedEditorClientAsync), diger testlerin
        // ayni sifreyle tekrar login olabilmesi icin degisiklik kalici olmamali.
        var revertResponse = await client.PutAsJsonAsync("/api/v1/users/me/password", new { currentPassword = "Editor!Test456", newPassword = password });
        revertResponse.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ChangeOwnPassword_WithoutToken_ReturnsUnauthorized()
    {
        var client = factory.CreateClient();

        var response = await client.PutAsJsonAsync("/api/v1/users/me/password", new { currentPassword = "x", newPassword = "y" });

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ---- Yardımcılar ----

    private async Task<HttpClient> CreateAuthenticatedAdminClientAsync()
    {
        var client = factory.CreateClient();
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "Admin!Dev123" });
        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        return client;
    }

    private async Task<HttpClient> CreateAuthenticatedEditorClientAsync()
    {
        const string userName = "users-controller-editor-test";
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
