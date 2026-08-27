using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Concrete;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// NAMESPACE BILEREK Microsoft.Extensions.DependencyInjection secildi (kendi
// projemizin namespace'i degil). Bu sayede API projesi
// "using Isbak_SAR_Guide.DataAccess;" yazmadan AddDataAccess() metodunu
// gorebiliyor - cunku bu namespace zaten ImplicitUsings ile her yerde acik.
// Sonuc: API, DataAccess'in kendi namespace'ini hic tanimiyor.
namespace Microsoft.Extensions.DependencyInjection;

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<Isbak_SAR_GuideDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Asgari Identity kaydi: UserManager/RoleManager persistence'a burada
        // baglanir. AddIdentityCore (AddIdentity DEGIL) - cookie auth semasi
        // kaydetmiyoruz, JWT semasi API katmaninda ayrica kaydedilecek (6.1).
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                // Faz 9.3: 5 basarisiz denemeden sonra 15 dakika kilit. Sadece
                // burada AYARLAMAK yetmez - AuthService.LoginAsync bunu
                // AccessFailedAsync/IsLockedOutAsync ile acikca tetiklemeli
                // (UserManager.CheckPasswordAsync tek basina kilit izlemez).
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.Lockout.AllowedForNewUsers = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<Isbak_SAR_GuideDbContext>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
