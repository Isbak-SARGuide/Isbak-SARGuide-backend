using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Concrete;

// Primary constructor kullanilmiyor: dbContext hem EfRepository<T>'nin temel
// olusturucusuna hem de FindOrphansAsync'in ihtiyac duydugu ContentBlock
// DbSet'ine gerekiyor - ikisi birden (CS9107) primary constructor parametre
// yakalamasiyla celisiyor, bu yuzden acik olusturucu + kendi alani kullanilir.
public class MediaRepository : EfRepository<Media>, IMediaRepository
{
    private readonly DbSet<ContentBlock> _contentBlocks;

    public MediaRepository(Isbak_SAR_GuideDbContext dbContext) : base(dbContext)
    {
        _contentBlocks = dbContext.Set<ContentBlock>();
    }

    public async Task<Media?> FindByChecksumAsync(string checksum, CancellationToken cancellationToken = default) =>
        await DbSet.FirstOrDefaultAsync(m => m.Checksum == checksum, cancellationToken);

    public async Task<IReadOnlyList<Media>> FindOrphansAsync(DateTime olderThanUtc, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(m => m.CreatedAt < olderThanUtc)
            .Where(m => !_contentBlocks.Any(b => b.MediaId == m.Id))
            .ToListAsync(cancellationToken);
}
