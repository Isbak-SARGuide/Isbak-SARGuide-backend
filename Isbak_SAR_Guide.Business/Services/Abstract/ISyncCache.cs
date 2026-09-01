namespace Isbak_SAR_Guide.Business.Services.Abstract;

/// <summary>
/// Bir kitabin EN SON yayinina ait manifest/snapshot JSON'unu bellekte tutar
/// (12.2). Bu ikisi, ETag'in (12.4) aksine belirli bir versiyona degil
/// "bookId'nin guncel yayini"na isaret eder - yani mutable bir gosterge,
/// yeni bir publish/rollback olunca degisir. Bu yuzden Invalidate,
/// PublishingService'in tek commit noktasindan (FinalizeAsync) cagirilir;
/// TTL sadece savunma amacli bir guvenlik agi, asil tazelik garantisi
/// event-driven invalidation'dan gelir.
/// </summary>
public interface ISyncCache
{
    string? GetManifest(int bookId);

    string? GetSnapshot(int bookId);

    void SetManifest(int bookId, string manifestJson);

    void SetSnapshot(int bookId, string snapshotJson);

    /// <summary>
    /// Bir kitabin hem manifest hem snapshot girdisini birlikte gecersiz kilar -
    /// ikisi ayni yayin olayina bagli, ayri ayri invalidate edilmeleri icin bir
    /// sebep yok.
    /// </summary>
    void Invalidate(int bookId);
}
