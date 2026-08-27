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

    /// <summary>
    /// Faz 6: bir Media'yi silmeden once hala kullanimda mi diye kontrol icin.
    /// Soft-delete interceptor'i FK'daki OnDelete(SetNull)'i tetiklemez (gercek
    /// DELETE degil UPDATE yapar) - bu yuzden MediaService bu kontrolu acikca
    /// yapmali, aksi halde bir ContentBlock gorunmez bir Media'ya sahipmis gibi
    /// tutarsiz kalabilir.
    /// </summary>
    Task<bool> AnyWithMediaIdAsync(int mediaId, CancellationToken cancellationToken = default);
}
