using AramaKurtarma.DataAccess.Context;
using AramaKurtarma.DataAccess.Repositories.Abstract;
using AramaKurtarma.DataAccess.Repositories.Concrete;
using AramaKurtarma.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// NAMESPACE BILEREK Microsoft.Extensions.DependencyInjection secildi (kendi
// projemizin namespace'i degil). Bu sayede API projesi
// "using AramaKurtarma.DataAccess;" yazmadan AddDataAccess() metodunu
// gorebiliyor - cunku bu namespace zaten ImplicitUsings ile her yerde acik.
// Sonuc: API, DataAccess'in kendi namespace'ini hic tanimiyor.
namespace Microsoft.Extensions.DependencyInjection;

public static class DataAccessServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AramaKurtarmaDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        // Asgari Identity kaydi: UserManager/RoleManager persistence'a burada
        // baglanir. AddIdentityCore (AddIdentity DEGIL) - cookie auth semasi
        // kaydetmiyoruz, JWT semasi API katmaninda ayrica kaydedilecek (6.1).
        services
            .AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<AramaKurtarmaDbContext>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        return services;
    }
}
