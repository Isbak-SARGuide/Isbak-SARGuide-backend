using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

public interface IModuleRepository : IRepository<Module>
{
    /// <summary>
    /// Bir kitabin modullerini sayfali/filtreli ceker (Faz 5 CMS liste ucu).
    /// DisplayOrder'a gore siralanir - admin panelde de gercek gorunum sirasi.
    /// </summary>
    Task<(IReadOnlyList<Module> Items, int TotalCount)> GetPagedAsync(
        int bookId,
        int page,
        int pageSize,
        bool? isPublished,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir kitabin TUM modullerini (sayfalamasiz) ceker - reorder'in sibling-set
    /// dogrulamasi icin gerekli (istekteki id listesi gercek settekiyle birebir
    /// eslesmeli, aksi halde bazi moduller pozisyonsuz kalir).
    /// </summary>
    Task<IReadOnlyList<Module>> FindAllByBookIdAsync(int bookId, CancellationToken cancellationToken = default);
}
