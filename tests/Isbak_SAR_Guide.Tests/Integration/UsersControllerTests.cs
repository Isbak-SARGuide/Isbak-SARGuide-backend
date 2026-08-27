using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
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
