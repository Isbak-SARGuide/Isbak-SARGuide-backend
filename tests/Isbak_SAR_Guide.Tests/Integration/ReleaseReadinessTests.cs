using System.Net;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

[Collection("Api")]
public class ReleaseReadinessTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_ReturnsHealthy_WithoutAuthentication()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task HealthReady_ReturnsHealthy_WhenDatabaseIsReachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Response_IncludesSecurityHeaders_OnSuccessResponse()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
        response.Headers.GetValues("X-Frame-Options").ShouldContain("DENY");
        response.Headers.GetValues("Referrer-Policy").ShouldContain("strict-origin-when-cross-origin");
        response.Headers.GetValues("Permissions-Policy").ShouldContain("camera=(), microphone=(), geolocation=()");
    }

    [Fact]
    public async Task Response_IncludesSecurityHeaders_OnUnauthorizedResponse()
    {
        // Fallback policy'nin uretttigi 401 gibi, pipeline'in erken kesildigi
        // yanitlarda da baslik garanti edilmis olmali (bkz. UseSecurityHeaders yorumu).
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/books");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("X-Content-Type-Options").ShouldContain("nosniff");
    }
}
