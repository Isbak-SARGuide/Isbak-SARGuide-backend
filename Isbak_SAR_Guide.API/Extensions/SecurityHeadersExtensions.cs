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
        app.Use((context, next) =>
        {
            // Dogrudan burada set etmek yerine OnStarting'e kayit ediyoruz:
            // ExceptionHandlerMiddleware, GlobalExceptionHandler'i cagirmadan
            // once Response.Clear() yapar (bu da Headers.Clear() cagirir) -
            // pipeline'da bu noktadan SONRA tekrar buraya donulmuyor, yani
            // dogrudan set edilen basliklar 500'lerde kaybolur. OnStarting
            // callback'leri ise yanit gercekten gonderilmeden HEMEN once
            // calisir (Clear()'dan SONRA), boylece 500 dahil her yanitta
            // baslik garanti edilmis olur.
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
                return Task.CompletedTask;
            });

            return next();
        });

        return app;
    }
}
