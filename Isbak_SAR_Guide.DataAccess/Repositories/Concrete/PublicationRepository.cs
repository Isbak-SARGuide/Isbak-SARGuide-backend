using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Concrete;

/// <summary>
/// EfRepository&lt;T&gt;'den turemez (BaseEntity kisiti); DbSet'i kendisi tutar.
/// </summary>
public class PublicationRepository(Isbak_SAR_GuideDbContext dbContext) : IPublicationRepository
{
    private readonly DbSet<BookPublication> _publications = dbContext.Set<BookPublication>();

    public async Task<int> GetLatestVersionAsync(int bookId, CancellationToken cancellationToken = default) =>
        // (int?) cast'i: bos kumede MaxAsync exception firlatir, nullable
        // overload null doner - SQL tarafinda yine tek bir MAX sorgusudur.
        await _publications
            .Where(p => p.BookId == bookId)
            .MaxAsync(p => (int?)p.Version, cancellationToken) ?? 0;

    public async Task<IReadOnlyList<int>> GetActiveContentIdsAsync(int bookId, int version, CancellationToken cancellationToken = default) =>
        // IsDeleted filtresi elle: PublishedContent'te bilerek HasQueryFilter
        // yok (tombstone'lar delta feed'inde gorunmek zorunda) - global
        // filtre burada kurtarmaz, kurtarmamali da.
        await dbContext.Set<PublishedContent>()
            .Where(pc => pc.BookId == bookId && pc.Version == version && !pc.IsDeleted)
            .Select(pc => pc.ContentId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(BookPublication publication, CancellationToken cancellationToken = default) =>
        await _publications.AddAsync(publication, cancellationToken);
}
