using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.DataAccess.Repositories.Abstract;

/// <summary>
/// Yayin defteri (BookPublication) erisimi. Bilerek IRepository&lt;T&gt;'den
/// turemiyor: BookPublication bir BaseEntity degil (immutable - soft delete /
/// update kavrami yok) ve Update/Remove/FindAll bir yayin gecmisi icin
/// anlamsiz. Dar arayuz, yanlis kullanimi derleme aninda imkansiz kilar.
/// </summary>
public interface IPublicationRepository
{
    /// <summary>
    /// Kitabin en son yayin versiyonunu doner; kitap hic yayinlanmamissa 0.
    /// Boylece cagiran taraf her zaman +1 yapar ve ilk yayin 1 olur.
    /// Versiyonun gercegin kaynagi bu tablodur (mutable Book.Version degil).
    /// </summary>
    Task<int> GetLatestVersionAsync(int bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni yayini ekler. PublishedContents koleksiyonu doldurulmus gelirse
    /// EF, cocuk satirlari navigation uzerinden ayni SaveChanges'te insert
    /// eder - PublishedContent icin ayri bir repo bilerek yok.
    /// </summary>
    Task AddAsync(BookPublication publication, CancellationToken cancellationToken = default);
}
