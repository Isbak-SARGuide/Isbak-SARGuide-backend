using System.Text.Json;
using Isbak_SAR_Guide.API.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Unit;

/// <summary>
/// Faz 12.5: bu sinif hicbir yerde dogrudan test edilmiyordu (yalnizca
/// entegrasyon testlerinde beklenen Result-hatalari tetikleniyor, gercek
/// bir unhandled exception hicbir zaman olusturulmuyordu). Genuinely
/// beklenmedik hatalarin RFC7807 ProblemDetails'e cevrilmesi projenin tek
/// global catch noktasi - bkz. CLAUDE.md "Genuinely unexpected exceptions".
/// </summary>
public class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_WritesProblemDetails_With500Status()
    {
        // Arrange
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        var handled = await handler.TryHandleAsync(context, new InvalidOperationException("beklenmedik"), CancellationToken.None);

        // Assert
        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status500InternalServerError);

        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var problemDetails = await JsonSerializer.DeserializeAsync<ProblemDetails>(context.Response.Body);
        problemDetails!.Status.ShouldBe(StatusCodes.Status500InternalServerError);
        problemDetails.Title.ShouldBe("Beklenmeyen bir hata olustu.");
    }

    [Fact]
    public async Task TryHandleAsync_NeverLeaksExceptionMessage_IntoResponseBody()
    {
        // Arrange - istisna mesaji ic detay tasiyabilir (dosya yolu, SQL vb.);
        // yanit govdesine hic sizmamali (CLAUDE.md "Error messages don't leak
        // sensitive data").
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        // Act
        await handler.TryHandleAsync(
            context,
            new InvalidOperationException("C:\\gizli\\yol\\connection-string-icerir"),
            CancellationToken.None);

        // Assert
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.ShouldNotContain("gizli");
        body.ShouldNotContain("connection-string");
    }
}
