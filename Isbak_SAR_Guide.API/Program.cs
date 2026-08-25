using Isbak_SAR_Guide.API.Middleware;
using Asp.Versioning;
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

builder.Services
    .AddDataAccess(builder.Configuration)
    .AddBusiness();

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
}

app.UseHttpsRedirection();

app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
