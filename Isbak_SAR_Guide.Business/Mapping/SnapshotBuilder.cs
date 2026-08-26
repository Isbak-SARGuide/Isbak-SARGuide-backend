using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Isbak_SAR_Guide.Business.DTOs.Sync;
using Isbak_SAR_Guide.Entities.Content;

namespace Isbak_SAR_Guide.Business.Mapping;

/// <summary>
/// Domain agacini (Book -> Modules -> Contents -> Blocks) sync sozlesmesi
/// DTO'larina cevirir ve kanonik serilestirme + checksum uretir. Hem SyncService
/// hem PublishingService buradan beslenir - sema tek yerde yasar, ayrisamaz.
///
/// Statik ve saf: durumu yok, bagimliligi yok, ayni girdiye her zaman ayni
/// cikti. Checksum idempotency'si (6.5) buna dayanir; bu yuzden koleksiyonlar
/// serilestirilmeden once DTO seviyesinde deterministik siralanir
/// (DisplayOrder + Id tiebreak) - garanti sorguda degil, burada durur.
/// </summary>
public static class SnapshotBuilder
{
    /// <summary>
    /// Kanonik form - PayloadJson/ManifestJson ve checksum'lar bu options ile
    /// uretilir. Ilke: kanonik form = wire format - saklanan bayt, servis
    /// edilecek bayttir, arada hicbir donusum yoktur.
    ///
    /// BU OPTIONS DONMUSTUR: herhangi bir degisiklik, yayinlanmis tum
    /// checksum'lari gecersiz kilar ve mobil dogrulamayi kirar.
    ///
    /// CamelCase: 5.0'da donan wire sozlesmesiyle hizali ("title", "moduleId") -
    /// Faz 4'te PayloadJson'in deserialize edilmeden aynen gecirilmesinin sarti.
    /// UnsafeRelaxedJsonEscaping: Turkce karakterler \uXXXX yerine oldugu gibi
    /// (UTF-8) yazilir. Bu JSON hicbir zaman HTML icine ham gomulmez (API-only);
    /// gomulecek olursa bu karar yeniden degerlendirilmeli.
    /// </summary>
    private static readonly JsonSerializerOptions _canonicalOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static SyncSnapshotDto BuildSnapshot(Book book)
    {
        var bookDto = new SyncBookDto(
            book.Id, book.Title, book.Slug, book.Description, book.LanguageCode, book.Version);

        var orderedModules = book.Modules
            .OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id)
            .ToList();

        var modules = orderedModules
            .Select(m => new SyncModuleDto(m.Id, m.BookId, m.Name, m.Description, m.DisplayOrder))
            .ToList();

        var contents = orderedModules
            .SelectMany(m => m.Contents.OrderBy(c => c.DisplayOrder).ThenBy(c => c.Id))
            .Select(BuildContentDto)
            .ToList();

        return new SyncSnapshotDto(book.Version, bookDto, modules, contents);
    }

    /// <summary>
    /// Publishing'in de giris noktasi: PayloadJson, content basina bu DTO'nun
    /// kanonik serilestirmesidir.
    /// </summary>
    public static SyncContentDto BuildContentDto(Content content) =>
        new(
            content.Id,
            content.ModuleId,
            content.Title,
            content.Summary,
            content.DisplayOrder,
            content.Blocks
                .OrderBy(b => b.DisplayOrder).ThenBy(b => b.Id)
                .Select(BuildBlockDto)
                .ToList(),
            content.VariantGroupKey,
            content.VariantLabel);

    /// <summary>
    /// Snapshot'tan manifest uretir: media ozetleri (DistinctBy Id). publishedAt
    /// ve checksum disaridan gelir - "gercek bir kez hesaplanir, sonra akar":
    /// publish ayni ani hem buraya hem PublishedAt kolonuna, ayni checksum'i
    /// hem buraya hem Checksum kolonuna gecirir. Checksum'i icerde hesaplamak
    /// ikinci bir serialize demek olurdu (tek-serialize kurali).
    /// </summary>
    public static SyncManifestDto BuildManifest(SyncSnapshotDto snapshot, DateTime publishedAt, string checksum)
    {
        var media = snapshot.Contents
            .SelectMany(c => c.Blocks)
            .Select(b => b.Media)
            .OfType<MediaSummaryDto>()
            .DistinctBy(m => m.Id)
            .ToList();

        return new SyncManifestDto(
            snapshot.Book.Id,
            snapshot.Version,
            publishedAt,
            snapshot.Contents.Count,
            media,
            checksum);
    }

    public static string Serialize<T>(T value) =>
        JsonSerializer.Serialize(value, _canonicalOptions);

    /// <summary>
    /// Kanonik JSON'un SHA-256 ozeti (hex). Iceride Serialize'i cagirir -
    /// "checksum, payload'in ozetidir" sozu ancak ikisi ayni serilestirmeyi
    /// paylastigi surece dogru kalir; ayri serialize cagrisina izin verme.
    /// </summary>
    public static string ComputeChecksum<T>(T value) => ComputeChecksum(Serialize(value));

    /// <summary>
    /// Evrensel invariant: her PublishedContent satirinda (tombstone dahil)
    /// Checksum, PayloadJson kolonunun AYNEN SHA-256'sidir. Mobil her satiri
    /// tek tip dogrular - tombstone icin istisna ogrenmesi gerekmez.
    /// Bu overload ayni zamanda "bir kez serialize et, ham checksum'i al"
    /// akisina izin verir (cift serialize maliyetini kaldirir).
    /// </summary>
    public static string ComputeChecksum(string json)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes);
    }

    private static SyncContentBlockDto BuildBlockDto(ContentBlock block)
    {
        var mediaDto = block.Media is null
            ? null
            : new MediaSummaryDto(block.Media.Id, block.Media.StoragePath, block.Media.Checksum, block.Media.FileSize);

        return new SyncContentBlockDto(block.Id, block.Type, block.Text, block.DataJson, mediaDto, block.DisplayOrder);
    }
}
