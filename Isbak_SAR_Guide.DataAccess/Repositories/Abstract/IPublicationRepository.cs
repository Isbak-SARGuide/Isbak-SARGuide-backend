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
    /// Belirli bir versiyonun ManifestJson'unu doner; o versiyon yoksa null.
    /// Delta'nin medya diff'i icin: fromVersion'daki manifest ile guncel
    /// manifest karsilastirilir. Projection - diger kolonlar cekilmez.
    /// </summary>
    Task<string?> GetManifestJsonAsync(int bookId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Content basina EN SON durumu, YALNIZCA Version > fromVersion olan
    /// satirlar arasindan doner (journal modeli, 7.3-a/c). Ayni korele-MAX
    /// deseni ama disaridan bir filtre daha: outer satir hem (Version >
    /// fromVersion) hem (o content'in TUM zamanlarindaki mutlak en yuksek
    /// versiyonu) olmali. Bu iki kosul birlikte dogru sonucu garanti eder:
    /// bir content'in son degisikligi fromVersion'dan eskiyse hic donmez
    /// (zaten degismemis); yeniyse tek satir doner - o da mutlaka en son
    /// durumdur (versiyonlar content basina hep artan sirada yazilir).
    /// </summary>
    Task<IReadOnlyList<PublishedContentChange>> GetChangedRowsSinceAsync(int bookId, int fromVersion, CancellationToken cancellationToken = default);

    /// <summary>
    /// Son yayinin ManifestJson'unu doner; hic yayin yoksa null. Projection
    /// bilerek: SnapshotJson'i (megabaytlik kolon) HIC cekmez - Select,
    /// FirstOrDefault'tan once SQL'e iner (SELECT "ManifestJson" ... LIMIT 1),
    /// buyuk kolon diskten okunmaz, aga cikmaz. Manifest mobilin en sik
    /// cagrisi - entity'yi cekip .ManifestJson okumak her istekte koca
    /// snapshot'i bosuna tasirdi.
    /// </summary>
    Task<string?> GetLatestManifestJsonAsync(int bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Son yayinin SnapshotJson'unu doner; hic yayin yoksa null.
    /// GetLatestManifestJsonAsync'in aynasi: projection bilerek -
    /// ManifestJson'i (ve diger kolonlari) HIC cekmez, SELECT "SnapshotJson"
    /// ... LIMIT 1 olarak iner.
    /// </summary>
    Task<string?> GetLatestSnapshotJsonAsync(int bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Belirli bir versiyonun SnapshotJson'unu doner; o versiyon yoksa null.
    /// Faz 12.6 rollback icin: GetManifestJsonAsync'in aynasi ama SnapshotJson
    /// icin - geri alinacak versiyonun TAM icerigi lazim, sadece manifesti degil.
    /// </summary>
    Task<string?> GetSnapshotJsonAsync(int bookId, int version, CancellationToken cancellationToken = default);

    /// <summary>
    /// Content basina yayin gunlugundeki en son satirin ozetini doner
    /// (greatest-per-group). Journal modelinin temeli: satir tablosu tam
    /// kopya degil degisiklik gunlugu oldugu icin "v'deki satirlar" sorusu
    /// yanlis soru olurdu - degismeyen content'in son satiri eski bir
    /// versiyondadir. Ilk publish'te bos liste doner - ozel dal gerekmez.
    /// </summary>
    Task<IReadOnlyList<PublishedContentState>> GetLatestContentStatesAsync(int bookId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Yeni yayini ekler. PublishedContents koleksiyonu doldurulmus gelirse
    /// EF, cocuk satirlari navigation uzerinden ayni SaveChanges'te insert
    /// eder - PublishedContent icin ayri bir repo bilerek yok.
    /// </summary>
    Task AddAsync(BookPublication publication, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kitabin tum yayin gecmisini (en yeniden eskiye) doner - rollback UI'inin
    /// "hangi versiyona donulebilir" listesi icin (web ekibinin geri bildirimi,
    /// bkz. Frontend-Notlar-ve-Oneriler.md madde 9b). SnapshotJson'a HIC
    /// dokunmaz - GetLatestManifestJsonAsync ile ayni projection ilkesi.
    /// </summary>
    Task<IReadOnlyList<PublicationHistoryRow>> GetHistoryAsync(int bookId, CancellationToken cancellationToken = default);
}
