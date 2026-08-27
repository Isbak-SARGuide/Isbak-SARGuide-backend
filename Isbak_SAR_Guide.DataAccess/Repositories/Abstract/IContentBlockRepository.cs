using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

public interface IContentBlockRepository : IRepository<ContentBlock>
{
    /// <summary>
    /// Bir icerigin bloklarini sayfali ceker. isPublished filtresi yok -
    /// ContentBlock'ta IsPublished alani yok (yayin durumu Content seviyesinde).
    /// </summary>
    Task<(IReadOnlyList<ContentBlock> Items, int TotalCount)> GetPagedAsync(
        int contentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Reorder'in sibling-set dogrulamasi icin - IModuleRepository'deki gerekce ayni.</summary>
    Task<IReadOnlyList<ContentBlock>> FindAllByContentIdAsync(int contentId, CancellationToken cancellationToken = default);
}
