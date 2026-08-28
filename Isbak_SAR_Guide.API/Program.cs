using System.Threading.RateLimiting;
using Asp.Versioning;
using Isbak_SAR_Guide.API.Middleware;
using Isbak_SAR_Guide.Business.Common;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"));

builder.Services.AddControllers();

builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.ReportApiVersions = true;
        // Versiyon SADECE URL segmentinden okunur (/api/v1/...). AssumeDefaultVersionWhenUnspecified
        // gerekmiyor cunku her controller'da [ApiVersion] zaten acikca yazili, header/query-string
        // gibi ek kaynaklari taramaya gerek yok - analyzer'in AV0015/AV0016 uyarilari bu yuzden.
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV";
        options.SubstituteApiVersionInUrl = true;
    });

builder.Services.AddOpenApi();

// Statik medya (PNG/JPG) zaten sikistirilmis - sadece JSON gövdeleri icin acik.
// EnableForHttps: sync/API JSON trafigi HTTPS uzerinden gidiyor, orada da sikistirma istiyoruz.
// BREACH riski degerlendirildi: access/refresh token'lar (LoginResponseDto)
// ayni govdede client-etkileyebilir alanlarla (UserName/FullName) birlikte
// donuyor, ama auth cerez DEGIL Bearer token ile yapiliyor - saldirganin
// cross-origin istegi tarayiciya credential'i otomatik ekletemedigi icin
// klasik BREACH senaryosu (CSRF benzeri, cerez tabanli) burada gecerli
// degil. Bu yuzden HTTPS'te sikistirma acik tutuluyor.
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

    options.AddDefaultPolicy(policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddApiAuthentication(builder.Configuration);

// Faz 9.3: sadece kimlik-dogrulama uclari (login/refresh) icin - IP basina
// dakikada N deneme, "login" adiyla ayri bir politika (AuthController'da
// [EnableRateLimiting("login")] ile opt-in). Faz 12.8: TUM istekler icin
// (ozellikle /sync/* - AllowAnonymous, kimlik dogrulamasi yok, roadmap'in
// "tek risk" dedigi yuzey) GlobalLimiter ile ek, daha gevsek bir taban limiti.
// Ikisi TOPLANMAZ, BIRLIKTE uygulanir: login/refresh istegi hem global hem
// login limitinden gecmeli - login limiti daha siki oldugu icin pratikte o
// once tetiklenir.
builder.Services.Configure<LoginRateLimitOptions>(builder.Configuration.GetSection(LoginRateLimitOptions.SectionName));
builder.Services.Configure<GlobalRateLimitOptions>(builder.Configuration.GetSection(GlobalRateLimitOptions.SectionName));

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Deger IOptions'tan istek-zamaninda okunur (Program.cs baslarken bir kez
    // degil) - ApiFactory testlerde PostConfigure<LoginRateLimitOptions> ile
    // bunu cok yuksek bir sayiyla ezer (StorageOptions'taki desenle ayni,
    // bkz. tests/.../ApiFactory.cs). Program baslarken tek seferlik okusaydik
    // test-zamanli PostConfigure'un araya girecegi bir an olmazdi.
    options.AddPolicy("login", httpContext =>
    {
        var rateLimitOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<LoginRateLimitOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                QueueLimit = 0,
            });
    });

    // GlobalLimiter, EnableRateLimiting/RequireRateLimiting ile opt-in gerekmeden
    // TUM endpoint'lere otomatik uygulanir - named policy'lerin aksine.
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var rateLimitOptions = httpContext.RequestServices
            .GetRequiredService<IOptions<GlobalRateLimitOptions>>().Value;

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateLimitOptions.PermitLimit,
                Window = TimeSpan.FromSeconds(rateLimitOptions.WindowSeconds),
                QueueLimit = 0,
            });
    });
});

builder.Services
    .AddDataAccess(builder.Configuration)
    .AddBusiness(builder.Configuration);

// StorageOptions.BasePath appsettings'te goreli yazilir ("../storage") -
// process CWD'sine degil ContentRootPath'e gore mutlak yola burada bir kez
// cevrilir. Process CWD'ye guvenmek testlerde (WebApplicationFactory farkli
// bir CWD'den calisabilir) yanlis klasore yazar/okur.
builder.Services.PostConfigure<StorageOptions>(options =>
    options.BasePath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, options.BasePath)));

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    // Fallback policy her endpoint'i kilitler (deny-by-default) - MapOpenApi ve
    // Scalar'in urettigi uclar da buna dahil. API dokumani Development'ta bilerek
    // anonim; bu blok production'da hic calismadigi icin oraya sizma riski yok.
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference().AllowAnonymous();

    await app.Services.SeedDatabaseAsync();
    await app.Services.PublishSeedBookAsync();
}

if (!app.Environment.IsDevelopment())
{
    // HSTS'i Development'ta acmiyoruz - dev sertifikasi tarayicida STS olarak
    // kalicilastigi icin sonradan http'ye donmek istendiginde sorun cikartir.
    app.UseHsts();
}

// ONEMLI - reverse proxy eklenirse: hem login hem global rate limiter (yukarida,
// Faz 9.3 + Faz 12.8) IP'yi httpContext.Connection.RemoteIpAddress'ten okuyor.
// Su an compose.prod.yaml container'i DOGRUDAN disari aciyor (8080:8080), yani
// bu dogru IP'yi verir. Onune bir reverse proxy/load balancer konursa ve
// UseForwardedHeaders() (X-Forwarded-For, proxy'nin guvenilir IP'sine
// kisitlanmis) buraya eklenmezse, RemoteIpAddress her istekte proxy'nin IP'sine
// sabitlenir - butun kullanicilar TEK bir rate-limit havuzunu paylasir ve hem
// login brute-force korumasi hem /sync/* icin taban koruma sessizce devre disi
// kalir. Bkz. docs/Deployment.md.
app.UseHttpsRedirection();

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseSecurityHeaders();

// Medya dosyalarini web koku ("") altinda servis eder: StorageOptions.BasePath
// fiziksel kok, Media.StoragePath (orn. "media/2026/08/<guid>.png") bu koke
// GORELI - tek "/" eklemek dogru URL'i verir (SnapshotBuilder.BuildBlockDto).
// UseAuthentication/UseAuthorization'DAN ONCE: mobil/web okuyucu anonim,
// fallback policy'nin buraya hic ugramaması gerekiyor (Sync gibi).
var storageOptions = app.Services.GetRequiredService<IOptions<StorageOptions>>().Value;
var storageAbsolutePath = Path.GetFullPath(storageOptions.BasePath);
Directory.CreateDirectory(storageAbsolutePath);

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageAbsolutePath),
    RequestPath = "",
});

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

// Fallback policy her endpoint'i deny-by-default yaptigi icin health check'ler
// de AllowAnonymous olmadan 401 doner - orkestrasyon/monitoring araclari token
// tasimaz. "/health": liveness, hicbir dependency check'i calistirmaz (Predicate
// false) - sadece process'in istek karsiladigini kanitlar. "/health/ready":
// readiness, "ready" tag'li check'leri (su an sadece Postgres) calistirir.
// DisableRateLimiting: Faz 12.8'in global limiter'i olmadan da bu uclar
// AllowAnonymous oldugu icin herkese acik - ama orkestrasyon araci (k8s
// liveness/readiness probe, Docker HEALTHCHECK) gercek kullanici trafigiyle
// AYNI IP/partition'i paylasirsa (reverse proxy arkasinda - bkz. yukaridaki
// ONEMLI yorumu) global limite carpip 429 donebilir. Bu, orkestratorun
// uygulamayi "olu" sanip GEREKSIZ YENIDEN BASLATMASINA yol acar - health
// check'in kendisi rate limit'e tabi olmamali, canli dogrulamada yakalandi
// (dusuk bir PermitLimit ile manuel test, /health de /sync/* gibi 429 donuyordu).
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = _ => false,
}).AllowAnonymous().DisableRateLimiting();

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
}).AllowAnonymous().DisableRateLimiting();

app.Run();
