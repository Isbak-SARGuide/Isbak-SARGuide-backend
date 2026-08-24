using AramaKurtarma.DataAccess.Context;
using AramaKurtarma.DataAccess.Seed;
using AramaKurtarma.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AramaKurtarmaDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")));

// Asgari Identity kaydi: UserManager/RoleManager seed icin simdi gerekli.
// AddIdentityCore (AddIdentity DEGIL) bilerek secildi - cookie tabanli auth
// semasini kaydetmiyor. Tam JWT auth pipeline'i Faz 2 / gorev 6.1'de gelecek.
builder.Services
    .AddIdentityCore<ApplicationUser>()
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AramaKurtarmaDbContext>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    using var scope = app.Services.CreateScope();
    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
}

app.UseHttpsRedirection();

app.Run();
