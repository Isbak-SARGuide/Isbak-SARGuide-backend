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

    /// <summary>
    /// Web ekibinin geri bildirimi (Frontend-Notlar-ve-Oneriler.md madde 5):
    /// admin panelin "Icerikler" ekrani TUM modullerdeki tum content'leri tek
    /// listede gostermek icin GET /books/{bookId}/modules + her modul icin
    /// ayri GET /modules/{id}/contents (N+1) yapiyordu. Kitap genelinde DUZ
    /// (flat) bir sayfali/filtreli liste - Module.BookId uzerinden join.
    /// </summary>
    Task<(IReadOnlyList<Content> Items, int TotalCount)> GetPagedByBookIdAsync(
        int bookId,
        int page,
        int pageSize,
        bool? isPublished,
        CancellationToken cancellationToken = default);

    /// <summary>Reorder'in sibling-set dogrulamasi icin - IModuleRepository'deki gerekce ayni.</summary>
    Task<IReadOnlyList<Content>> FindAllByModuleIdAsync(int moduleId, CancellationToken cancellationToken = default);
}
