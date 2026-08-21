using AramaKurtarma.Entities.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AramaKurtarma.DataAccess.Context;

public class AramaKurtarmaDbContext : IdentityDbContext<ApplicationUser>
{
    public AramaKurtarmaDbContext(DbContextOptions<AramaKurtarmaDbContext> options)
        : base(options)
    {
    }
}
