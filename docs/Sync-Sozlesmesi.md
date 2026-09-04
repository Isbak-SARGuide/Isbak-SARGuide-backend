# Isbak SAR Guide — Mobile Sync Contract v1.3

Bu doküman, mobil uygulamanın (Flutter, ayrı repo `Isbak-SARGuide-mobile`) offline-first
senkronizasyon için kullandığı üç endpoint'in resmi sözleşmesidir. Backend ekibi ile
mobil ekip arasındaki kalıcı anlaşma budur — burada tarif edilen alan adları, tipleri ve
davranışlar backend'in **donmuş sözleşmesidir** (bkz. §9).

Admin CMS panelinin kullandığı içerik yönetimi/yayınlama uçları bu dokümanın kapsamı
DIŞINDADIR — onlar için ayrı bir sözleşme var: **[`CMS-API-Sozlesmesi.md`](CMS-API-Sozlesmesi.md)**.

---

## 1. Genel Bakış

Mobil uygulama tamamen offline çalışır: saha koşullarında şebeke yoktur, bu yüzden
içerik önceden indirilip cihazda (SQLite, `sqflite`) tutulur. Backend admin tarafında
içerik sürekli düzenlenir ama mobil bunu **hiç görmez** — sadece admin bir "yayınla"
(publish) eylemi yaptığında, o anki içeriğin donmuş bir kopyası (versiyonlu) mobile
açılır. Mobil ilk kurulumda tam paketi (`snapshot`) indirir, sonrasında sadece
**değişen** içeriği (`changes`) çeker — büyük bir el kitabını her güncellemede baştan
indirmemek için. Her indirilen paketin bozulmadığı SHA-256 checksum ile doğrulanır (§5).

## 2. Temel Bilgiler

- **Base URL deseni:** `/api/v{version}/sync` (şu an `v1`).
- **Medya base URL:** `media[].url` ve `ContentBlock.media.url` alanları **göreli**
  bir yoldur (örn. `media/kentsel-arama-kurtarma-el-kitabi/dosya.png`). Bu yol,
  sync endpoint'leriyle **AYNI host'un köküne** görelidir — `/api/v{version}` ön eki
  YOKTUR (`{host}/media/...`). Statik dosyalar backend'de web kökünde servis edilir,
  ayrı bir CDN/host yoktur.
- **Kimlik doğrulama: YOK.** Üç uç da anonim (`[AllowAnonymous]`) — mobil uygulama
  hiçbir zaman token taşımaz, göndermez.
- Tüm başarılı yanıtlar `Content-Type: application/json`, alan adları **camelCase**.
- Türkçe karakterler her zaman düz UTF-8 baytları olarak gönderilir, Unicode kaçış
  dizisi (`\uXXXX`) OLARAK DEĞİL.
- **ETag / `If-None-Match` (v1.3, additive, opsiyonel):** Üç uç da bir `ETag` başlığı
  döner (kitap id'si + yayın versiyonu). İstemci bir sonraki istekte aynı değeri
  `If-None-Match` ile geri gönderirse ve içerik hâlâ güncelse sunucu gövdesiz
  `304 Not Modified` döner — mobil için bant genişliği tasarrufu.

## 3. Endpoint'ler

### 3.1 `GET /sync/manifest?bookId={id}`

Her açılışta önce bu çağrılır — küçük, ucuz. Amaç: "sunucudaki versiyon benim
elimdekinden farklı mı?" sorusunun hızlı cevabı.

```json
{
  "bookId": 1,
  "version": 16,
  "publishedAt": "2026-08-27T11:11:12.1633669Z",
  "contentCount": 97,
  "media": [
    { "id": 1, "url": "media/kentsel-arama-kurtarma-el-kitabi/dosya.png", "checksum": "AC5F...", "size": 37898, "thumbnailUrl": null }
  ],
  "checksum": "DD8076ED4D90BA850535F9987D7C7F20F4FEC2AA32C847A324F276C79EE920B5"
}
```

| Alan | Tip | Açıklama |
|---|---|---|
| `bookId` | int | Kitabın kimliği |
| `version` | int | Son yayının sürüm numarası |
| `publishedAt` | string (ISO 8601 UTC) | Yayının yapıldığı an |
| `contentCount` | int | Bu yayında **hayatta olan** (silinmemiş) content sayısı |
| `media` | array | Bu yayında referans verilen medya özetleri |
| `checksum` | string (hex, 64 karakter) | `snapshot` yanıtının SHA-256'sı — §5'teki doğrulama için |

`thumbnailUrl` doluysa küçük bir WebP önizlemenin göreli yoludur; `null` ise önizleme
yok demektir, istemci `url`'i kullanmalıdır.

### 3.2 `GET /sync/snapshot?bookId={id}`

İlk kurulumda bir kez çekilir — tam paket. Mobil bunu SQLite'a yazar, sonraki
güncellemeler `changes` ile gelir.

```json
{
  "version": 16,
  "book": { "id": 1, "title": "Kentsel Arama Kurtarma El Kitabı", "slug": "kentsel-arama-kurtarma-el-kitabi", "description": "...", "languageCode": "tr", "version": 16 },
  "modules": [ { "id": 1, "bookId": 1, "name": "BSAFE", "description": "...", "displayOrder": 0 } ],
  "contents": [
    {
      "id": 25,
      "moduleId": 5,
      "title": "Temel Düğümler — 8 Şeklinde Döngü Düğüm",
      "summary": "...",
      "displayOrder": 5,
      "blocks": [
        { "id": 89, "type": 1, "text": "...", "dataJson": null, "media": null, "displayOrder": 0 },
        { "id": 90, "type": 2, "text": null, "dataJson": null, "media": { "id": 13, "url": "media/.../figur8-dugum.png", "checksum": "...", "size": 48522, "thumbnailUrl": null }, "displayOrder": 1 }
      ],
      "variantGroupKey": "temel-dugumler",
      "variantLabel": "F8"
    }
  ]
}
```

| Alan | Tip | Açıklama |
|---|---|---|
| `version` | int | Bu snapshot'ın ait olduğu sürüm |
| `book` | object | `id`, `title`, `slug`, `description`, `languageCode`, `version` |
| `modules` | array | `id`, `bookId`, `name`, `description`, `displayOrder` |
| `contents` | array | `id`, `moduleId`, `title`, `summary`, `displayOrder`, `blocks[]`, `variantGroupKey`, `variantLabel` |
| `contents[].blocks` | array | `id`, `type` (§4), `text`, `dataJson`, `media`, `displayOrder` |

**Notlar:**
- Silinmiş content'ler bu listede **hiç yer almaz** — snapshot her zaman o anki
  hayatta-olan durumu temsil eder.
- **`variantGroupKey` / `variantLabel`:** Çoğu content'te ikisi de `null`. Bir konunun
  birden fazla varyantı varsa (örn. bir düğüm türünün F8/F9/TH/ABK dört varyantı), hepsi
  **aynı** `variantGroupKey`'i taşır ve mobil bunları **tek bir sekmeli sayfada**
  birleştirmelidir — her sekmenin etiketi kendi `variantLabel`'ı, sekme sırası kendi
  `displayOrder`'ıdır. Üst listede (modülün "Konular" listesi), aynı grubu paylaşan
  varyantlar arasında **en küçük `displayOrder`'a sahip varyantın** `title`/`summary`'si
  gösterilir — ayrı bir `variantGroupTitle` alanı yoktur.

### 3.3 `GET /sync/changes?bookId={id}&fromVersion={n}`

> **Bu endpoint bir GÜNLÜKTÜR, tam kopya DEĞİLDİR.** Yalnızca `fromVersion`'dan sonra
> **gerçekten değişmiş** (eklenmiş, düzenlenmiş veya silinmiş) content'ler döner. Bir
> content son yayından beri hiç değişmediyse, bu yanıtta **hiç yer almaz** — tasarımın
> ta kendisi.

```json
{
  "fromVersion": 17,
  "toVersion": 18,
  "book": { "id": 1, "title": "...", "slug": "...", "description": "...", "languageCode": "tr", "version": 18 },
  "upsertedContents": [],
  "deletedContentIds": [100],
  "modules": [ { "id": 1, "bookId": 1, "name": "BSAFE", "description": "...", "displayOrder": 0 } ],
  "addedMedia": [],
  "removedMediaIds": [94]
}
```

| Alan | Tip | Açıklama |
|---|---|---|
| `fromVersion` | int | İsteğinizdeki değer (yankı) |
| `toVersion` | int | Sunucunun güncel sürümü — sonraki `changes` isteğinizde bunu `fromVersion` olarak kullanın |
| `book` | object | `toVersion`'daki güncel kitap durumu — `modules` gibi koşulsuz her yanıtta gelir |
| `upsertedContents` | array | **Sadece değişen** content'ler — şekli `snapshot.contents[]` ile birebir aynı |
| `deletedContentIds` | array\<int\> | Silinen content id'leri |
| `modules` | array | **Her zaman güncel modül listesinin TAMAMI** — diff yapılmaz, toptan gelir |
| `addedMedia` | array | Yeni eklenen veya checksum'ı değişen medya |
| `removedMediaIds` | array\<int\> | Artık referans verilmeyen medya id'leri |

**Notlar:**
- `modules` alanı içerik hiç değişmemiş olsa bile **her zaman doludur** — değişiklik
  takibi content seviyesinde yapılır.
- Silinmiş bir content daha sonra geri gelirse, normal bir `upsertedContents` öğesi
  olarak görünür — `deletedContentIds`'te değil.
- Modül/content id'leri bilerek ardışık olmayabilir (geliştirme sırasında oluşturulup
  silinen kayıtlar id boşluğu bırakır) — sıralama için her zaman `displayOrder`'a
  güvenin, `id`'ye değil.

## 4. Blok Tipleri

`ContentBlock.type` bir tamsayıdır:

| `type` | Ad | `text` | `dataJson` | `media` |
|---|---|---|---|---|
| 1 | Text | dolu | boş | boş |
| 2 | Image | genelde boş | boş | dolu (görsel) |
| 3 | Video | genelde boş | boş | dolu (video) — **henüz gerçek içerik yok, provisional** |
| 4 | Animation | genelde boş | dolu (adım dizisi) | dolu olabilir — **henüz gerçek içerik yok, provisional** |
| 5 | Warning | dolu | genelde boş | boş |
| 6 | Table | boş | dolu (satır/sütun verisi) | boş |

`dataJson` her zaman bir **string**'dir (JSON içinde JSON) — ayrıca `JSON.parse`
edilmesi gerekir, otomatik açılmaz. Table örneği:

```json
{ "id": 15, "type": 6, "text": null, "dataJson": "{\"rows\": [[\"İşi Durdurma\", \"1 Uzun Sinyal\"]], \"headers\": [\"İşaret\", \"Sinyal\"]}", "media": null, "displayOrder": 0 }
```

## 5. Bütünlük Doğrulama (Checksum)

1. `GET /sync/manifest?bookId={id}` çekin, `checksum` alanını saklayın.
2. `GET /sync/snapshot?bookId={id}` çekin — yanıtı **ham baytlar** olarak alın, JSON'a
   parse ETMEDEN önce.
3. O ham baytların SHA-256'sını hesaplayın, hex string'e çevirin, **büyük harfe**
   çevirin (`ToUpperInvariant()`).
4. Manifest'teki `checksum` ile karşılaştırın.
5. Tutmuyorsa: indirme bozulmuş demektir, yeniden indirin. Tutuyorsa: snapshot'ı
   JSON'a parse edip local veritabanına yazabilirsiniz.

`changes` yanıtının böyle bir bütün-checksum'ı **yoktur** (bkz. §9 "bilerek yok").
Medya dosyaları kendi `checksum` alanlarıyla ayrı ayrı doğrulanır.

## 6. Hata Sözleşmesi

RFC 9457 `ProblemDetails`. Mobil uygulama hata ayrımını **`status`'a değil `title`'a
bakarak** yapmalıdır:

| `title` | HTTP | Anlamı | Hangi uçlarda |
|---|---|---|---|
| `Sync.BookNotFound` | 404 | Bu id'de bir kitap hiç yok | Üçü de |
| `Sync.NotPublished` | 404 | Kitap var ama hiç yayınlanmamış (meşru durum — "içerik hazırlanıyor" gösterin) | Üçü de |
| `Sync.InvalidFromVersion` | 400 | `fromVersion` geçersiz: negatif, güncel sürümden büyük, veya sunucunun artık bilmediği bir sürüm | Sadece `changes` |

`Sync.InvalidFromVersion` alırsanız: lokal sürüm bilgisi sunucuyla artık uyuşmuyor
demektir. Delta'ya güvenmeyi bırakıp tam `snapshot`'a düşün.

## 7. Önerilen Mobil Akış

**İlk kurulum:**
```
1. GET /sync/manifest?bookId={id}
2. GET /sync/snapshot?bookId={id}
3. §5'teki checksum doğrulamasını yap (tutmuyorsa yeniden indir)
4. snapshot'ı parse edip local veritabanına (SQLite) yaz
5. contents[].blocks[].media dolu olan her blok için medyayı indir, checksum ile doğrula
6. Local "mevcut sürüm"ü manifest.version olarak kaydet
```

**Güncelleme:**
```
1. GET /sync/manifest?bookId={id}
2. manifest.version > local sürüm mü? (hayırsa bitti)
3. GET /sync/changes?bookId={id}&fromVersion={local sürüm}
   (400 Sync.InvalidFromVersion alırsan → "İlk kurulum"a dön)
4. upsertedContents'teki her content'i local'de ekle/güncelle
5. deletedContentIds'teki her id'yi local'den sil
6. modules dizisini local modül tablosunun TAMAMININ yerine yaz
7. addedMedia'daki her medyayı indir/üzerine yaz, removedMediaIds'i sil
8. Local "mevcut sürüm"ü changes.toVersion olarak kaydet
```

**Senkronizasyon ne zaman tetiklenir?** Mobil istemcinin gerçek davranışı: sadece
**uygulama soğuk açılışında** (tamamen kapatılıp yeniden açıldığında) — arka planda
bekleyen bir örnek otomatik yeniden senkronize olmaz, kullanıcı elle bir "yenile"
eylemi de yapamaz (v1 kapsamında yok). Bu, admin panelinden "Yayınla" dendikten sonra
sahadaki bir cihazın güncel içeriği görmesi için uygulamanın tamamen kapatılıp yeniden
açılması gerektiği anlamına gelir — bkz. [`Kullanici-Kilavuzu-Saha.md`](Kullanici-Kilavuzu-Saha.md).

## 8. Kenar Durumları

| Durum | Davranış | Mobil ne yapmalı |
|---|---|---|
| `fromVersion=0` | Geçerli — ama tüm canlı içeriği VE geçmişteki tüm tombstone'ları taşır | Önerilmez; ilk kurulumda onun yerine `snapshot` kullanın |
| `fromVersion` == güncel sürüm | 200, boş listeler döner — `modules` yine doludur | Hata değil, "zaten güncelsin" anlamına gelir |
| Silinen bir content sonra geri gelirse | `upsertedContents`'te sıradan bir upsert gibi görünür | `deletedContentIds`'te aramayın; upsert gibi işleyin |
| Modül adı/açıklaması değişti, içerik değişmedi | `changes` yanıtı gelir, `modules` güncel adı taşır | Her `changes` yanıtında `modules`'ü toptan uygulayın |
| Modül/content id'leri ardışık olmayabilir | Geliştirme sırasında oluşturulup silinen kayıtlar id boşluğu bırakır | Sıralama için her zaman `displayOrder`'a güvenin |

## 9. Sözleşme Evrim Kuralı

v1.0'dan sonra bu sözleşmeye **sadece alan eklenebilir** (additive). Var olan bir
alanın adı, tipi veya anlamı **değişmez**; hiçbir alan **kaldırılmaz**. Mobil
uygulama, JSON parse ederken bilmediği/tanımadığı bir alanı sessizce **yok
saymalıdır**. Kırıcı bir değişiklik gerekirse yeni bir sözleşme `/api/v2/sync`
altında açılır.

**Bilerek sözleşmeye alınmayan:** parça-başına checksum (`changes` içindeki her
content için ayrı checksum) — payload'ın kendi checksum'ını taşıması tavuk-yumurta
problemi yaratırdı; bütünlük TLS'e ve medyanın kendi checksum'ına emanettir.

## 10. Sürüm Geçmişi

| Sürüm | Not |
|---|---|
| v1.0 | İlk teslim — `manifest`, `snapshot`, `changes` (journal modeli, `modules` alanı dahil) |
| v1.1 | Additive: `contents[]`'e `variantGroupKey`/`variantLabel` eklendi |
| v1.2 | Additive: `changes`'e `book` alanı eklendi; medya base URL ve varyant grubu üst-liste kuralı netleştirildi |
| v1.3 | Additive: üç uca da `ETag` yanıt başlığı + `If-None-Match` desteği eklendi |

---

*Admin CMS sözleşmesi için → [`CMS-API-Sozlesmesi.md`](CMS-API-Sozlesmesi.md).
Mimari kararların gerekçesi için → [`Mimari.md`](Mimari.md).*
