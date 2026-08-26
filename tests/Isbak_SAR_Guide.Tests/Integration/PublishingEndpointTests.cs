using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Business.DTOs.Publishing;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Ince endpoint testleri - motor semantigi 18 testle PublishingTests'te
/// kanitli; burasi sadece "boru bagli" der: route -> auth -> claim -> servis
/// -> DTO. 401/403 ayrimi kritik: 401 "kimsin bilmiyorum", 403 "kimsin
/// biliyorum, yetkin yok" - Editor testi urun kararinin tek kanitidir.
/// </summary>
[Collection("Api")]
public class PublishingEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Publish_WithoutToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = factory.CreateClient();
        var bookId = await CreateBookAsync();

        // Act
        var response = await client.PostAsync($"/api/v1/books/{bookId}/publish", content: null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Publish_AsAdmin_ReturnsOkWithVersion()
    {
        // Arrange
        var client = factory.CreateClient();
        var bookId = await CreateBookAsync();
        await AuthenticateAsync(client, "admin", "Admin!Dev123");

        // Act
        var response = await client.PostAsync($"/api/v1/books/{bookId}/publish", content: null);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PublishResultDto>();
        result!.Version.ShouldBe(1);
        result.BookId.ShouldBe(bookId);
    }

    [Fact]
    public async Task Publish_AsEditor_ReturnsForbidden()
    {
        // Arrange - seed'de Editor rolu var ama kullanicisi yok; yarat.
        var client = factory.CreateClient();
        var bookId = await CreateBookAsync();

        var (userName, password) = await CreateEditorUserAsync();
        await AuthenticateAsync(client, userName, password);

        // Act
        var response = await client.PostAsync($"/api/v1/books/{bookId}/publish", content: null);

        // Assert - 403, 401 DEGIL: kimligi gecerli, yetkisi yok.
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // ---- Yardimcilar ----

    private async Task<int> CreateBookAsync()
    {
        using var scope = factory.Services.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var book = new Book
        {
            Title = "Endpoint Test Kitabı",
            Slug = $"endpoint-test-{Guid.NewGuid():N}",
        };

        await unitOfWork.Books.AddAsync(book);
        await unitOfWork.SaveChangesAsync();
        return book.Id;
    }

    private async Task<(string UserName, string Password)> CreateEditorUserAsync()
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // Testler DB'yi paylasir - benzersiz kullanici adi sart.
        var userName = $"editor-{Guid.NewGuid():N}";
        const string password = "Editor!Dev123";

        var editor = new ApplicationUser
        {
            UserName = userName,
            Email = $"{userName}@isbak-sar-guide.local",
            EmailConfirmed = true,
            FullName = "Test Editörü",
        };

        var createResult = await userManager.CreateAsync(editor, password);
        createResult.Succeeded.ShouldBeTrue(string.Join(", ", createResult.Errors.Select(e => e.Description)));

        await userManager.AddToRoleAsync(editor, RoleNames.Editor);
        return (userName, password);
    }

    private static async Task AuthenticateAsync(HttpClient client, string userName, string password)
    {
        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName, password });
        loginResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
    }
}
