using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Concrete;

public class ContentBlockRepository(Isbak_SAR_GuideDbContext dbContext)
    : EfRepository<ContentBlock>(dbContext), IContentBlockRepository
{
    public async Task<(IReadOnlyList<ContentBlock> Items, int TotalCount)> GetPagedAsync(
        int contentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(b => b.ContentId == contentId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(b => b.DisplayOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<ContentBlock>> FindAllByContentIdAsync(int contentId, CancellationToken cancellationToken = default) =>
        await DbSet
            .Where(b => b.ContentId == contentId)
            .OrderBy(b => b.DisplayOrder)
            .ToListAsync(cancellationToken);
}
