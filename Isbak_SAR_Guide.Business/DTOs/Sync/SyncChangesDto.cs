namespace Isbak_SAR_Guide.Business.DTOs.Sync;

/// <summary>
/// Delta yanitinin donmus sekli (v1.0). UpsertedContents/DeletedContentIds
/// yalnizca DEGISEN content'leri tasir (journal modeli, 7.3-a) - degismeyen
/// content delta'da HIC yer almaz; bu "delta = tam indirme" tuzagini kapatan
/// kuralin sekle yansimasidir.
///
/// Modules additive alan (7.3-b): her yanit ToVersion'in TAM guncel modul
/// listesini tasir - istemci kendi modul tablosunu toptan degistirir (modul
/// sayisi kucuk, diff maliyetine degmez; toptan-degistir idempotenttir).
/// Alan eklemek JSON sozlesmelerinde kirici degildir (istemci bilmedigi
/// alani yok sayar); resmi v1.0 teslimi (7.6) henuz yapilmadigi icin bu
/// son ucuz an.
///
/// Book additive alan (Faz 13.2): manifest/snapshot'in aksine changes hic
/// kitap meta verisi tasimiyordu - kitabin kendi basligi/aciklamasi bir
/// yayinda degisirse istemci bunu ancak tam snapshot cekerek ogrenirdi.
/// Modules'la ayni gerekce ve ayni "son ucuz an": ToVersion'daki GUNCEL
/// Book durumu, degisip degismedigine bakilmaksizin her yanitta gelir.
///
/// UYARI - drift riski: gercek wire uretimi BU RECORD UZERINDEN DEGIL,
/// Business/Mapping/SyncChangesJsonWriter'dadir (zarf elle Utf8JsonWriter
/// ile yazilir; content parcalari ve Modules dizisi WriteRawValue ile ham
/// kopyalanir - donmus PayloadJson/SnapshotJson baytlarina dokunulmaz).
/// Bu record SADECE sozlesmenin sekil dokumantasyonudur; alan degisikligi
/// ikisini BIRLIKTE guncellemeyi gerektirir.
///
/// Bilincli olarak YOK: parca-basina checksum. Delta'nin butun-checksum'i
/// yoktur; bir bayt oynasa tavuk-yumurta olurdu (payload'in sekli, kendi
/// checksum'ini de tasiyamaz). v1 kurali: delta butunlugu TLS'e emanettir,
/// medya kendi checksum'iyla (MediaSummaryDto), snapshot manifest checksum'iyla
/// dogrulanir. Ihtiyac kanitlanirsa additive bir v1.1 alani olur.
/// </summary>
public sealed record SyncChangesDto(
    int FromVersion,
    int ToVersion,
    SyncBookDto Book,
    IReadOnlyList<SyncContentDto> UpsertedContents,
    IReadOnlyList<int> DeletedContentIds,
    IReadOnlyList<SyncModuleDto> Modules,
    IReadOnlyList<MediaSummaryDto> AddedMedia,
    IReadOnlyList<int> RemovedMediaIds);
