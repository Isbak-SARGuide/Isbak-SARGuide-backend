namespace Microsoft.AspNetCore.Builder;

/// <summary>
/// Faz 11.2: saf JSON API + statik medya servisi icin anlamli olan
/// baslikların minimum kumesi. Content-Security-Policy bilerek eklenmedi -
/// hicbir HTML/JS servis edilmiyor (Scalar/OpenAPI sadece Development'ta ve
/// zaten AllowAnonymous), CSP'nin koruyacagi bir yuzey yok.
/// </summary>
public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
    {
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers["X-Content-Type-Options"] = "nosniff";
            headers["X-Frame-Options"] = "DENY";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

            await next();
        });

        return app;
    }
}
