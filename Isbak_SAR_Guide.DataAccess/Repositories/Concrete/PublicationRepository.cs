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

    public async Task<string?> GetManifestJsonAsync(int bookId, int version, CancellationToken cancellationToken = default) =>
        await _publications
            .Where(p => p.BookId == bookId && p.Version == version)
            .Select(p => p.ManifestJson)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PublishedContentChange>> GetChangedRowsSinceAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default)
    {
        var rows = dbContext.Set<PublishedContent>();

        return await rows
            .Where(pc => pc.BookId == bookId
                && pc.Version > fromVersion
                && pc.Version == rows
                    .Where(inner => inner.BookId == bookId && inner.ContentId == pc.ContentId)
                    .Max(inner => (int?)inner.Version))
            .Select(pc => new PublishedContentChange(pc.ContentId, pc.PayloadJson, pc.IsDeleted))
            .ToListAsync(cancellationToken);
    }

    public async Task<string?> GetSnapshotJsonAsync(int bookId, int version, CancellationToken cancellationToken = default) =>
        await _publications
            .Where(p => p.BookId == bookId && p.Version == version)
            .Select(p => p.SnapshotJson)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<string?> GetLatestManifestJsonAsync(int bookId, CancellationToken cancellationToken = default) =>
        await _publications
            .Where(p => p.BookId == bookId)
            .OrderByDescending(p => p.Version)
            .Select(p => p.ManifestJson)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<string?> GetLatestSnapshotJsonAsync(int bookId, CancellationToken cancellationToken = default) =>
        await _publications
            .Where(p => p.BookId == bookId)
            .OrderByDescending(p => p.Version)
            .Select(p => p.SnapshotJson)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<LatestPublicationSummary?> GetLatestSummaryAsync(int bookId, CancellationToken cancellationToken = default) =>
        await _publications
            .Where(p => p.BookId == bookId)
            .OrderByDescending(p => p.Version)
            .Select(p => new LatestPublicationSummary(p.Id, p.Version, p.Checksum, p.PublishedAt))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<PublishedContentState>> GetLatestContentStatesAsync(int bookId, CancellationToken cancellationToken = default)
    {
        var rows = dbContext.Set<PublishedContent>();

        // Greatest-per-group: content basina en yuksek versiyonlu satir.
        // Korele MAX alt sorgusu - EF guvenilir cevirir; Postgres DISTINCT ON
        // icin raw SQL'e inmeyi gerektirecek bir performans kanitimiz yok.
        return await rows
            .Where(pc => pc.BookId == bookId && pc.Version == rows
                .Where(inner => inner.BookId == bookId && inner.ContentId == pc.ContentId)
                .Max(inner => (int?)inner.Version))
            .Select(pc => new PublishedContentState(pc.ContentId, pc.Checksum, pc.IsDeleted))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BookPublication publication, CancellationToken cancellationToken = default) =>
        await _publications.AddAsync(publication, cancellationToken);

    public async Task<IReadOnlyList<PublicationHistoryRow>> GetHistoryAsync(int bookId, CancellationToken cancellationToken = default) =>
        await _publications
            .Where(p => p.BookId == bookId)
            .OrderByDescending(p => p.Version)
            .Select(p => new PublicationHistoryRow(p.Id, p.Version, p.PublishedBy.UserName!, p.ManifestJson))
            .ToListAsync(cancellationToken);
}
