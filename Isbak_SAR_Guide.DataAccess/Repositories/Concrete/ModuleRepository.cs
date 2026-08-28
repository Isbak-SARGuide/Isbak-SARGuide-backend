using Isbak_SAR_Guide.DataAccess.Context;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Content;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Concrete;

public class ModuleRepository(Isbak_SAR_GuideDbContext dbContext)
    : EfRepository<Module>(dbContext), IModuleRepository
{
    public async Task<(IReadOnlyList<Module> Items, int TotalCount)> GetPagedAsync(
        int bookId,
        int page,
        int pageSize,
        bool? isPublished,
        CancellationToken cancellationToken = default)
    {
        var query = DbSet.Where(m => m.BookId == bookId);

        if (isPublished.HasValue)
        {
            query = query.Where(m => m.IsPublished == isPublished.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .AsNoTracking()
            .OrderBy(m => m.DisplayOrder)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    // AsNoTracking: iki cagiran da ya salt-okunur (Create'in max(DisplayOrder)
    // hesabi) ya da ReorderHelper.ApplyAsync'in her entity'yi ACIKCA Update()
    // ile tekrar attach ettigi reorder akisi - hicbiri onceden tracked olmayi
    // gerektirmiyor.
    public async Task<IReadOnlyList<Module>> FindAllByBookIdAsync(int bookId, CancellationToken cancellationToken = default) =>
        await DbSet
            .AsNoTracking()
            .Where(m => m.BookId == bookId)
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(cancellationToken);
}
