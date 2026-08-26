# Isbak SAR Guide — Mobile Sync Contract v1.0

Bu doküman, mobil uygulamanın offline-first senkronizasyon için kullanacağı
üç endpoint'in resmi sözleşmesidir. Backend ekibi ile mobil ekip arasındaki
kalıcı anlaşma budur — burada tarif edilen alan adları, tipleri ve
davranışlar backend'in **donmuş sözleşmesidir** (bkz. §9).

Bu dokümandaki her JSON örneği, çalışan API'den alınan **gerçek bir
yanıttır** — elle yazılmamıştır. Uzun listeler (16 content'ten 2'si gibi)
okunabilirlik için kısaltılmıştır, ama kısaltılan kısım hep gerçek verinin
bir alt kümesidir; hiçbir alan adı, sıralama ya da biçim uydurulmamıştır.

---

## 1. Genel Bakış

Mobil uygulama tamamen offline çalışır: saha koşullarında şebeke yoktur, bu
yüzden içerik önceden indirilip cihazda (SQLite) tutulur. Backend admin
tarafında içerik sürekli düzenlenir ama mobil bunu **hiç görmez** — sadece
admin bir "yayınla" (publish) eylemi yaptığında, o anki içeriğin donmuş bir
kopyası (versiyonlu) mobile açılır. Mobil ilk kurulumda tam paketi
(`snapshot`) indirir, sonrasında sadece **değişen** içeriği (`changes`)
çeker — büyük bir el kitabını her güncellemede baştan indirmemek için.
Her indirilen paketin bozulmadığı SHA-256 checksum ile doğrulanır (§5).

---

## 2. Temel Bilgiler

- **Base URL deseni:** `/api/v{version}/sync` (şu an `v1`) — gerçek host
  ortama göre ayrıca verilir (dev/staging/prod).
- **Kimlik doğrulama: YOK.** Üç uç da anonim (`[AllowAnonymous]`) — mobil
  uygulama hiçbir zaman token taşımaz, göndermez.
- Tüm başarılı yanıtlar `Content-Type: application/json`, alan adları
  **camelCase**.
- **Önemli:** Sunucudan gelen gerçek yanıtlar bu dokümandaki gibi girintili
  (pretty-printed) DEĞİLDİR — tek satır, boşluksuz, kompakt JSON'dur.
  Checksum'lar tam olarak o kompakt baytlar üzerinden hesaplanır (§5).
  Bu dokümanda sadece okunabilirlik için girintilenmiştir; içerik (alan
  adları, sıra, değerler) birebir gerçek yanıtla aynıdır.
- Türkçe karakterler (`ş`, `ı`, `ğ`, `ö`, `ü`, `ç`, ...) her zaman düz
  UTF-8 baytları olarak gönderilir, Unicode kaçış dizisi (backslash-u
  formatı) OLARAK DEĞİL. Bazı JSON serializer'ları varsayılan olarak
  ASCII-dışı karakterleri kaçış dizisine çevirir — bu API öyle yapmaz.
  Çoğu JSON kütüphanesi ikisini de doğru parse eder, ama loglarda ve
  veritabanında karşılaşacağınız gerçek bayt dizisi düz UTF-8'dir.

---

## 3. Endpoint'ler

### 3.1 `GET /sync/manifest?bookId={id}`

Her açılışta önce bu çağrılır — küçük, ucuz. Amaç: "sunucudaki versiyon
benim elimdekinden farklı mı?" sorusunun hızlı cevabı.

**Örnek yanıt** (gerçek, `bookId=1` için):

```json
{
  "bookId": 1,
  "version": 1,
  "publishedAt": "2026-08-25T22:42:41.6500119Z",
  "contentCount": 16,
  "media": [],
  "checksum": "04EC0289C4B44D32457406B36043494A9A645FA61D9E4545523B2D36E05B5118"
}
```

> Bu kitapta henüz medya yok, o yüzden `media` boş dizi olarak geldi. Medya
> içeren bir kitapta bu dizinin her öğesi `id`, `url`, `checksum`, `size`
> alanlarını taşır (aşağıdaki alan tablosuna bakın).

**Alan tablosu:**

| Alan | Tip | Açıklama |
|---|---|---|
| `bookId` | int | Kitabın kimliği |
| `version` | int | Son yayının sürüm numarası |
| `publishedAt` | string (ISO 8601 UTC) | Yayının yapıldığı an |
| `contentCount` | int | Bu yayında **hayatta olan** (silinmemiş) content sayısı |
| `media` | array | Bu yayında referans verilen medya özetleri: `id`, `url`, `checksum`, `size` |
| `checksum` | string (hex, 64 karakter) | `snapshot` yanıtının SHA-256'sı — §5'teki doğrulama için |

**Notlar:**
- Kitap hiç yoksa veya hiç yayınlanmamışsa bkz. §6 (hata sözleşmesi).

### 3.2 `GET /sync/snapshot?bookId={id}`

İlk kurulumda bir kez çekilir — tam paket. Mobil bunu SQLite'a yazar,
sonraki güncellemeler `changes` ile gelir.

**Örnek yanıt** (gerçek, kısaltılmış — 4 modülün tamamı, 16 content'ten
ilk 2'si):

```json
{
  "version": 1,
  "book": {
    "id": 1,
    "title": "Kentsel Arama Kurtarma El Kitabı",
    "slug": "kentsel-arama-kurtarma-el-kitabi",
    "description": "Kentsel arama kurtarma operasyonlarında görev alan ekipler için temel başvuru kaynağı.",
    "languageCode": "tr",
    "version": 1
  },
  "modules": [
    {
      "id": 1,
      "bookId": 1,
      "name": "Enkaz Altında Arama Teknikleri",
      "description": "Çökme sonrası kayıp kişilerin tespiti için kullanılan sistematik arama yöntemleri.",
      "displayOrder": 0
    },
    {
      "id": 2,
      "bookId": 1,
      "name": "Bina Stabilite Değerlendirmesi",
      "description": "Müdahale öncesi yapının güvenlik açısından hızlı değerlendirilmesi.",
      "displayOrder": 1
    },
    {
      "id": 3,
      "bookId": 1,
      "name": "İlk Yardım ve Triyaj",
      "description": "Çoklu kayıp/yaralı durumlarında öncelik belirleme ve temel müdahale.",
      "displayOrder": 2
    },
    {
      "id": 4,
      "bookId": 1,
      "name": "Ekip İçi İletişim ve Koordinasyon",
      "description": "Saha operasyonlarında ekipler arası bilgi akışı ve komuta zinciri.",
      "displayOrder": 3
    }
  ],
  "contents": [
    {
      "id": 1,
      "moduleId": 1,
      "title": "Sesli ve Görsel Arama Yöntemi",
      "summary": "Elektronik ekipman olmadan uygulanabilen temel arama tekniği.",
      "displayOrder": 0,
      "blocks": [
        {
          "id": 1,
          "type": 1,
          "text": "Sesli arama, enkaz alanında belirli aralıklarla tam sessizlik sağlanarak kayıp kişilerden gelebilecek ses veya vurma sinyallerinin dinlenmesi esasına dayanır.",
          "dataJson": null,
          "media": null,
          "displayOrder": 0
        },
        {
          "id": 2,
          "type": 5,
          "text": "Sesli arama sirasinda tum ekipman ve jeneratorler durdurulmalidir.",
          "dataJson": "{\"severity\": \"high\"}",
          "media": null,
          "displayOrder": 1
        }
      ]
    },
    {
      "id": 2,
      "moduleId": 1,
      "title": "Arama Köpekleri ile Koordinasyon",
      "summary": "Köpek ekipleriyle çalışırken ekip güvenliği ve alan yönetimi.",
      "displayOrder": 1,
      "blocks": [
        {
          "id": 3,
          "type": 1,
          "text": "Arama köpeği enkaz üzerinde çalışırken alanda gereksiz personel bulundurulmamalı, köpek eğitmeninin verdiği işaretler tüm ekiple paylaşılmalıdır.",
          "dataJson": null,
          "media": null,
          "displayOrder": 0
        }
      ]
    }
  ]
}
```

**Alan tablosu:**

| Alan | Tip | Açıklama |
|---|---|---|
| `version` | int | Bu snapshot'ın ait olduğu sürüm |
| `book` | object | `id`, `title`, `slug`, `description`, `languageCode`, `version` |
| `modules` | array | `id`, `bookId`, `name`, `description`, `displayOrder` |
| `contents` | array | `id`, `moduleId`, `title`, `summary`, `displayOrder`, `blocks[]` |
| `contents[].blocks` | array | `id`, `type` (§4), `text`, `dataJson`, `media`, `displayOrder` |

**Notlar:**
- `contents` içindeki her blok, `media` alanı doluysa `MediaSummaryDto`
  şeklinde bir obje taşır (`id`, `url`, `checksum`, `size`); boşsa `null`.
- Silinmiş content'ler bu listede **hiç yer almaz** — snapshot her zaman
  o anki hayatta-olan durumu temsil eder.

### 3.3 `GET /sync/changes?bookId={id}&fromVersion={n}`

> **Bu endpoint bir GÜNLÜKTÜR, tam kopya DEĞİLDİR.** Yalnızca
> `fromVersion`'dan sonra **gerçekten değişmiş** (eklenmiş, düzenlenmiş
> veya silinmiş) content'ler döner. Bir content son yayından beri hiç
> değişmediyse, bu yanıtta **hiç yer almaz** — bu bir hata değil, tasarımın
> ta kendisi. "Neden bazı content'ler hiç gelmiyor?" sorusunun cevabı budur.

**Örnek yanıt** (gerçek, `fromVersion=0` — yani "hiçbir şeyim yok, her şeyi
ver"; `upsertedContents` 16 öğeden ilk 2'si gösteriliyor):

```json
{
  "fromVersion": 0,
  "toVersion": 1,
  "upsertedContents": [
    {
      "id": 1,
      "moduleId": 1,
      "title": "Sesli ve Görsel Arama Yöntemi",
      "summary": "Elektronik ekipman olmadan uygulanabilen temel arama tekniği.",
      "displayOrder": 0,
      "blocks": [
        {
          "id": 1,
          "type": 1,
          "text": "Sesli arama, enkaz alanında belirli aralıklarla tam sessizlik sağlanarak kayıp kişilerden gelebilecek ses veya vurma sinyallerinin dinlenmesi esasına dayanır.",
          "dataJson": null,
          "media": null,
          "displayOrder": 0
        },
        {
          "id": 2,
          "type": 5,
          "text": "Sesli arama sirasinda tum ekipman ve jeneratorler durdurulmalidir.",
          "dataJson": "{\"severity\": \"high\"}",
          "media": null,
          "displayOrder": 1
        }
      ]
    },
    {
      "id": 2,
      "moduleId": 1,
      "title": "Arama Köpekleri ile Koordinasyon",
      "summary": "Köpek ekipleriyle çalışırken ekip güvenliği ve alan yönetimi.",
      "displayOrder": 1,
      "blocks": [
        {
          "id": 3,
          "type": 1,
          "text": "Arama köpeği enkaz üzerinde çalışırken alanda gereksiz personel bulundurulmamalı, köpek eğitmeninin verdiği işaretler tüm ekiple paylaşılmalıdır.",
          "dataJson": null,
          "media": null,
          "displayOrder": 0
        }
      ]
    }
  ],
  "deletedContentIds": [],
  "modules": [
    {
      "id": 1,
      "bookId": 1,
      "name": "Enkaz Altında Arama Teknikleri",
      "description": "Çökme sonrası kayıp kişilerin tespiti için kullanılan sistematik arama yöntemleri.",
      "displayOrder": 0
    },
    {
      "id": 2,
      "bookId": 1,
      "name": "Bina Stabilite Değerlendirmesi",
      "description": "Müdahale öncesi yapının güvenlik açısından hızlı değerlendirilmesi.",
      "displayOrder": 1
    },
    {
      "id": 3,
      "bookId": 1,
      "name": "İlk Yardım ve Triyaj",
      "description": "Çoklu kayıp/yaralı durumlarında öncelik belirleme ve temel müdahale.",
      "displayOrder": 2
    },
    {
      "id": 4,
      "bookId": 1,
      "name": "Ekip İçi İletişim ve Koordinasyon",
      "description": "Saha operasyonlarında ekipler arası bilgi akışı ve komuta zinciri.",
      "displayOrder": 3
    }
  ],
  "addedMedia": [],
  "removedMediaIds": []
}
```

**Alan tablosu:**

| Alan | Tip | Açıklama |
|---|---|---|
| `fromVersion` | int | İsteğinizdeki değer (yankı) |
| `toVersion` | int | Sunucunun güncel sürümü — sonraki `changes` isteğinizde bunu `fromVersion` olarak kullanın |
| `upsertedContents` | array | **Sadece değişen** content'ler — şekli `snapshot.contents[]` ile birebir aynı |
| `deletedContentIds` | array\<int\> | Silinen content id'leri |
| `modules` | array | **Her zaman güncel modül listesinin TAMAMI** (§8) |
| `addedMedia` | array | Yeni eklenen veya checksum'ı değişen medya (`id`, `url`, `checksum`, `size`) |
| `removedMediaIds` | array\<int\> | Artık referans verilmeyen medya id'leri |

**Notlar:**
- `modules` alanı, içerik hiç değişmemiş olsa bile **her zaman doludur** —
  değişiklik takibi content seviyesinde yapılır, modüller her yanıtta
  toptan gelir (§7, §8).
- Silinmiş bir content daha sonra geri gelirse, normal bir `upsertedContents`
  öğesi olarak görünür — `deletedContentIds`'te değil.

---

## 4. Blok Tipleri

`ContentBlock.type` alanı bir tamsayıdır (`Isbak_SAR_Guide.Entities/Content/Enums/ContentBlockType.cs`):

| `type` | Ad | `text` | `dataJson` | `media` |
|---|---|---|---|---|
| 1 | Text | dolu | boş (`null`) | boş (`null`) |
| 2 | Image | genelde boş | boş | dolu (görsel dosyası) |
| 3 | Video | genelde boş | boş | dolu (video dosyası) |
| 4 | Animation | genelde boş | dolu (animasyon parametreleri) | dolu olabilir |
| 5 | Warning | dolu | dolu (örn. `{"severity":"high"}`) | boş |
| 6 | Table | boş (`null`) | dolu (satır/sütun verisi) | boş |

**Gerçek Warning örneği** (`type=5`, yukarıdaki örnekte de görüldü):

```json
{
  "id": 2,
  "type": 5,
  "text": "Sesli arama sirasinda tum ekipman ve jeneratorler durdurulmalidir.",
  "dataJson": "{\"severity\": \"high\"}",
  "media": null,
  "displayOrder": 1
}
```

**Gerçek Table örneği** (`type=6`):

```json
{
  "id": 5,
  "type": 6,
  "text": null,
  "dataJson": "{\"rows\": [[\"V-Şekli\", \"Büyük, erişilebilir\"], [\"Kayma Tipi\", \"Dar, dikkatli giriş gerekir\"], [\"Tam Çökme\", \"Boşluk az, yüksek risk\"]], \"headers\": [\"Çökme Tipi\", \"Tipik Boşluk\"]}",
  "media": null,
  "displayOrder": 0
}
```

> `dataJson` her zaman bir **string**'dir (JSON içinde JSON) — kendi
> içeriğini ayrıca `JSON.parse` etmeniz gerekir, otomatik açılmaz.

Seed veride `Image`/`Video`/`Animation` örneği yok; bu üç tip için
`media` alanı dolu geldiğinde şekli §3.1'deki `MediaSummaryDto` iledir.

---

## 5. Bütünlük Doğrulama (Checksum)

İndirilen `snapshot`'ın bozulmadığından emin olmak için:

1. `GET /sync/manifest?bookId={id}` çekin, `checksum` alanını saklayın.
2. `GET /sync/snapshot?bookId={id}` çekin — yanıtı **ham baytlar** olarak
   alın, henüz JSON'a parse ETMEDEN.
3. O ham baytların SHA-256'sını hesaplayın, hex string'e çevirin.
4. Hex string'i **büyük harfe çevirip** (`ToUpperInvariant()` benzeri)
   manifest'teki `checksum` ile karşılaştırın — sunucu büyük harfli hex
   üretir (örn. `04EC0289...`, `04ec0289...` değil).
5. Tutmuyorsa: indirme bozulmuş demektir, yeniden indirin. Tutuyorsa:
   snapshot'ı JSON'a parse edip local veritabanına yazabilirsiniz.

**Bu tarifin gerçekten çalıştığının kanıtı** (2026-08-26 tarihinde,
gerçek `snapshot` yanıtı üzerinde elle doğrulandı):

```
SHA256(snapshot ham baytları) = 04EC0289C4B44D32457406B36043494A9A645FA61D9E4545523B2D36E05B5118
manifest.checksum              = 04EC0289C4B44D32457406B36043494A9A645FA61D9E4545523B2D36E05B5118
✓ eşleşiyor
```

> `changes` yanıtının böyle bir bütün-checksum'ı **yoktur** (§9'daki
> "bilerek yok" notuna bakın). Medya dosyaları kendi `checksum`
> alanlarıyla ayrı ayrı doğrulanır.

---

## 6. Hata Sözleşmesi

Hatalar standart [RFC 9110 `ProblemDetails`](https://www.rfc-editor.org/rfc/rfc9457) şeklinde döner. Gerçek örnek:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Sync.BookNotFound",
  "status": 404,
  "detail": "Id=999999 olan kitap bulunamadı.",
  "traceId": "00-163d01511ef29b3e652a4046672f22c3-0a2c53b93c50ddf4-00"
}
```

Mobil uygulama hata ayrımını **`status`'a değil `title`'a bakarak** yapmalı
— aynı HTTP durum kodunu paylaşan farklı anlamlar var:

| `title` (kod) | HTTP | Anlamı | Hangi uçlarda |
|---|---|---|---|
| `Sync.BookNotFound` | 404 | Bu id'de bir kitap hiç yok (yanlış id / konfigürasyon hatası) | Üçü de |
| `Sync.NotPublished` | 404 | Kitap var ama hiç yayınlanmamış (meşru durum — "içerik hazırlanıyor" gösterin) | Üçü de |
| `Sync.InvalidFromVersion` | 400 | `fromVersion` geçersiz: negatif, güncel sürümden büyük, veya sunucunun artık bilmediği bir sürüm | Sadece `changes` |

`Sync.InvalidFromVersion` alırsanız: lokal sürüm bilgisi sunucuyla artık
uyuşmuyor demektir (çok eski / bozulmuş). Delta'ya güvenmeyi bırakıp tam
`snapshot`'a düşün.

**Gerçek örnekler** (`Sync.NotPublished` ve `Sync.InvalidFromVersion`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Sync.NotPublished",
  "status": 404,
  "detail": "Kitap henüz yayınlanmadı.",
  "traceId": "00-a505ccfa2d0f5864d4fbdbac5455cb01-303066a5c2bec9d9-00"
}
```

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Sync.InvalidFromVersion",
  "status": 400,
  "detail": "Geçersiz sürüm numarası; tam senkronizasyon (snapshot) gerekli.",
  "traceId": "00-30fe33305781518f048ea4471a425454-0d1c74fb4b0683dd-00"
}
```

---

## 7. Önerilen Mobil Akış

**İlk kurulum** (cihazda hiç veri yok):

```
1. GET /sync/manifest?bookId={id}
2. GET /sync/snapshot?bookId={id}
3. §5'teki checksum doğrulamasını yap
   ✗ tutmuyorsa → snapshot'ı yeniden indir
   ✓ tutuyorsa  → devam et
4. snapshot'ı parse edip local veritabanına (SQLite) yaz
5. contents[].blocks[].media dolu olan her blok için:
   medyayı media.url'den indir, media.checksum ile doğrula
6. Local "mevcut sürüm"ü manifest.version olarak kaydet
```

**Güncelleme** (cihazda zaten bir sürüm var):

```
1. GET /sync/manifest?bookId={id}
2. manifest.version > local sürüm mü?
   ✗ hayır → güncelsin, bitti
   ✓ evet  → devam et
3. GET /sync/changes?bookId={id}&fromVersion={local sürüm}
   ✗ 400 Sync.InvalidFromVersion alırsan → §7 "İlk kurulum"a dön
     (versiyon geçmişin kaybolmuş/çok eski demektir)
   ✓ 200 alırsan → devam et
4. upsertedContents'teki her content'i local'de ekle/güncelle
5. deletedContentIds'teki her id'yi local'den sil
6. modules dizisini local modül tablosunun TAMAMININ yerine yaz
   (diff yapmaya gerek yok — modül sayısı küçük, toptan değiştirmek
   idempotent ve daha basit)
7. addedMedia'daki her medyayı indir/üzerine yaz, removedMediaIds'i sil
8. Local "mevcut sürüm"ü changes.toVersion olarak kaydet
```

---

## 8. Kenar Durumları

| Durum | Davranış | Mobil ne yapmalı |
|---|---|---|
| `fromVersion=0` | Geçerli — ama tüm canlı içeriği VE geçmişteki tüm tombstone'ları (silinmiş içerik kayıtlarını) taşır | Önerilmez; ilk kurulumda onun yerine `snapshot` kullanın |
| `fromVersion` == güncel sürüm | 200, boş listeler döner (`upsertedContents: []`, `deletedContentIds: []`) — `modules` yine doludur | Hata değil, "zaten güncelsin" anlamına gelir |
| Silinen bir content sonra geri gelirse | `upsertedContents`'te sıradan bir upsert gibi görünür | `deletedContentIds`'te aramayın; upsert gibi işleyin |
| Modül adı/açıklaması değişti, içerik değişmedi | `changes` yanıtı gelir (yeni bir sürüm açılmıştır), `upsertedContents`/`deletedContentIds` boş olabilir ama `modules` güncel adı taşır | Her `changes` yanıtında `modules`'ü toptan uygulayın (adım 6, §7) |

---

## 9. Sözleşme Evrim Kuralı

v1.0'dan sonra bu sözleşmeye **sadece alan eklenebilir** (additive). Var
olan bir alanın adı, tipi veya anlamı **değişmez**; hiçbir alan
**kaldırılmaz**. Mobil uygulama, JSON parse ederken bilmediği/tanımadığı
bir alanı sessizce **yok saymalıdır** — ileride eklenecek yeni alanlar
mevcut mobil sürümleri kırmamalıdır.

Kırıcı bir değişiklik gerekirse (alan kaldırma, tip değiştirme, anlam
değiştirme), yeni bir sözleşme `/api/v2/sync` altında açılır; `v1` bir
süre paralel yaşamaya devam eder.

**Bilerek sözleşmeye alınmayanlar** (v1.0 kapsamı dışı, gerekçesiyle):
- **Parça-başına checksum** (`changes` içindeki her content için ayrı
  checksum): payload'ın kendi checksum'ını taşıması tavuk-yumurta problemi
  yaratırdı. Bütünlük TLS'e ve medyanın kendi checksum'ına emanettir.
  İhtiyaç kanıtlanırsa additive bir v1.1 alanı olarak eklenir.

---

## 10. Sürüm Geçmişi

| Sürüm | Tarih | Not |
|---|---|---|
| v1.0 | 2026-08-26 | İlk teslim — `manifest`, `snapshot`, `changes` (journal modeli, additive `modules` alanı dahil) |
