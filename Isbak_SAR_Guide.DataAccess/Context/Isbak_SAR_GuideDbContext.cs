using Isbak_SAR_Guide.Entities.Common;
using Isbak_SAR_Guide.Entities.Content;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.DataAccess.Context;

public class Isbak_SAR_GuideDbContext : IdentityDbContext<ApplicationUser>
{
    public Isbak_SAR_GuideDbContext(DbContextOptions<Isbak_SAR_GuideDbContext> options)
        : base(options)
    {
    }

    public DbSet<Book> Books => Set<Book>();

    public DbSet<Module> Modules => Set<Module>();

    public DbSet<Content> Contents => Set<Content>();

    public DbSet<ContentBlock> ContentBlocks => Set<ContentBlock>();

    public DbSet<Media> Media => Set<Media>();

    public DbSet<BookPublication> BookPublications => Set<BookPublication>();

    public DbSet<PublishedContent> PublishedContents => Set<PublishedContent>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(Isbak_SAR_GuideDbContext).Assembly);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        StampAuditFields();
        return base.SaveChanges();
    }

    /// <summary>
    /// CreatedAt/UpdatedAt/DeletedAt'i her SaveChanges'te merkezi olarak
    /// damgalar - servis katmaninda unutulma riskini ortadan kaldirir.
    /// </summary>
    private void StampAuditFields()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    break;

                case EntityState.Deleted:
                    // Hard delete yerine soft delete: satiri silmek yerine
                    // isaretle. Entity Framework'un state'ini degistiriyoruz.
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = now;
                    entry.Entity.UpdatedAt = now;
                    break;
            }
        }
    }
}
