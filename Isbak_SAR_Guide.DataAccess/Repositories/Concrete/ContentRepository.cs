using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Concrete;

public class ContentRepository(Isbak_SAR_GuideDbContext dbContext)
    : EfRepository<Content>(dbContext), IContentRepository
{
    public async Task<(IReadOnlyList<Content> Items, int TotalCount)> GetPagedAsync(
        int moduleId,
        int page,
        int pageSize,
        bool? isPublished,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(c => c.ModuleId == moduleId);

        if (isPublished.HasValue)
        {
            query = query.Where(c => c.IsPublished == isPublished.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .AsNoTracking()
            .OrderBy(c => c.DisplayOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    // AsNoTracking: bkz. ModuleRepository.FindAllByBookIdAsync - ayni gerekce
    // (salt-okunur Create hesabi ya da ReorderHelper'in acikca UpdateProperty()
    // ile tek kolon isaretleyip yeniden attach ettigi reorder akisi).
    public async Task<IReadOnlyList<Content>> FindAllByModuleIdAsync(int moduleId, CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(c => c.ModuleId == moduleId)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken);
}
