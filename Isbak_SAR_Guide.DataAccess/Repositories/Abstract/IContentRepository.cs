using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

public interface IContentRepository : IRepository<Content>
{
    /// <summary>Bir modulun icerigini sayfali/filtreli ceker.</summary>
    Task<(IReadOnlyList<Content> Items, int TotalCount)> GetPagedAsync(
        int moduleId,
        int page,
        int pageSize,
        bool? isPublished,
        CancellationToken cancellationToken = default);

    /// <summary>Reorder'in sibling-set dogrulamasi icin - IModuleRepository'deki gerekce ayni.</summary>
    Task<IReadOnlyList<Content>> FindAllByModuleIdAsync(int moduleId, CancellationToken cancellationToken = default);
}
