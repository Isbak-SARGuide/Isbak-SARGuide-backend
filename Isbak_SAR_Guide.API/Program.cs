using System.Threading.RateLimiting;
using Asp.Versioning;
using Isbak_SAR_Guide.API.Middleware;
using Isbak_SAR_Guide.Business.Common;
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

// Add services to the container.
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
// dakikada N deneme. Global rate limiting bilerek kapsam disi (roadmap Faz 9
// Hardening - "Anonim sync endpoint'i tek risk", auth farkli/daha acil bir yuzey).
builder.Services.Configure<LoginRateLimitOptions>(builder.Configuration.GetSection(LoginRateLimitOptions.SectionName));

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

// Configure the HTTP request pipeline.
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

app.UseHttpsRedirection();

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

app.Run();
