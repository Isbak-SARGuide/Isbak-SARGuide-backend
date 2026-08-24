using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace AramaKurtarma.API.Middleware;

/// <summary>
/// Ongorulmemis (beklenmedik) exception'lari yakalayip RFC 7807 ProblemDetails
/// formatinda dondurur. Beklenen hatalar (NotFound, Validation vb.) buraya hic
/// dusmez - onlar Result pattern ile normal donus degeri olarak yonetiliyor.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Islenmeyen hata: {Message}", exception.Message);

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Beklenmeyen bir hata olustu.",
            Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
        };

        httpContext.Response.StatusCode = problemDetails.Status.Value;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
