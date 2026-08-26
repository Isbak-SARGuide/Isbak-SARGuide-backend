using System.Net;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Altyapinin (Testcontainers + WebApplicationFactory + migration + seed) gercekten
/// calistigini dogrulayan minimal bir test. Diger entegrasyon testleri icin ORNEK
/// olarak da kullanilabilir - ayni [Collection("Api")] + constructor deseni.
/// </summary>
[Collection("Api")]
public class SmokeTests(ApiFactory factory)
{
    [Fact]
    public async Task UnauthenticatedRequest_ToProtectedEndpoint_Returns401()
    {
        // Arrange
        var client = factory.CreateClient();

        // Act
        var response = await client.GetAsync("/api/v1/books");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
