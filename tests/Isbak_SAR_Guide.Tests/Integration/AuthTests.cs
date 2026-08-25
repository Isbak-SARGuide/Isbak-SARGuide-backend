using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Isbak_SAR_Guide.Business.DTOs.Auth;
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

        var loginResponse = await client.PostAsJsonAsync("/api/v1/auth/login", new { userName = "admin", password = "Admin!Dev12i3" });

        var loginResult = await loginResponse.Content.ReadFromJsonAsync<LoginResponseDto>();

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginResult!.AccessToken);
        var response = await client.GetAsync("api/v1/books");

        
    }
}
