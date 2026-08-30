# Isbak SAR Guide — Mobile Sync Contract v1.0

Bu doküman, mobil uygulamanın offline-first senkronizasyon için kullanacağı
üç endpoint'in resmi sözleşmesidir. Backend ekibi ile mobil ekip arasındaki
kalıcı anlaşma budur — burada tarif edilen alan adları, tipleri ve
davranışlar backend'in **donmuş sözleşmesidir** (bkz. §9).

Bu dokümandaki her JSON örneği, çalışan API'den alınan **gerçek bir
yanıttır** — elle yazılmamıştır. Uzun listeler (97 content'ten 2'si gibi)
okunabilirlik için kısaltılmıştır, ama kısaltılan kısım hep gerçek verinin
bir alt kümesidir; hiçbir alan adı, sıralama ya da biçim uydurulmamıştır.
Örnekler 2026-08-28'de, kitabın tamamı (10 modül, 97 konu) yayınlanmış
**v16** sürümüne karşı yeniden üretildi. §3.3'teki `changes` örneği,
`book` alanının eklenmesiyle (v1.2) 2026-08-29'da **v18**'e karşı ayrıca
tazelendi — diğer bölümlerdeki örnekler hâlâ v16'dandır, aradaki
versiyonlarda sadece bu belgeyle ilgisiz test verisi eklenip temizlendi.

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
- **Medya base URL:** `media[].url` ve `ContentBlock.media.url` alanları
  **göreli** bir yoldur (örn. `media/kentsel-arama-kurtarma-el-kitabi/dosya.png`).
  Bu yol, sync endpoint'leriyle **AYNI host'un köküne** görelidir — `/api/
  v{version}` ön eki YOKTUR. Yani tam URL, `{host}/media/...` şeklinde kurulur
  (örn. sync base'i `https://api.example.com/api/v1/sync` ise, medya
  `https://api.example.com/media/kentsel-arama-kurtarma-el-kitabi/dosya.png`
  olur). Statik dosyalar backend'de web kökünde servis edilir, ayrı bir CDN/
  host yoktur.
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

**Örnek yanıt** (gerçek, `bookId=1` için, `media` dizisi 93 öğeden ilk 3'üne
kısaltıldı):

```json
{
  "bookId": 1,
  "version": 16,
  "publishedAt": "2026-08-27T11:11:12.1633669Z",
  "contentCount": 97,
  "media": [
    {
      "id": 1,
      "url": "media/kentsel-arama-kurtarma-el-kitabi/sektor-ceyrek-diyagrami.png",
      "checksum": "AC5F5E0B6C81D6CEE012B91C7E9F03519D19337C80F0CB76F9B0D6DD24F1E4EB",
      "size": 37898
    },
    {
      "id": 2,
      "url": "media/kentsel-arama-kurtarma-el-kitabi/kat-seviyesi-gosterimi.png",
      "checksum": "C5BD4008343972696B1BBDD235F4EFDAF445CFDFD47E2807301B7D5F2A0AA5DD",
      "size": 56515
    },
    {
      "id": 3,
      "url": "media/kentsel-arama-kurtarma-el-kitabi/bolge-tanimlama-ornek.png",
      "checksum": "863B3CDED6F30FD97901D946EDED89B8ADC8DC7BEE6EBD5F486691D776682157",
      "size": 14119
    }
  ],
  "checksum": "DD8076ED4D90BA850535F9987D7C7F20F4FEC2AA32C847A324F276C79EE920B5"
}
```

> Gerçek yanıtta `media` dizisinde 93 öğe var (kitaptaki tüm görseller).
> Kitap ilk yayınlandığında henüz medyası yoksa bu dizi boş (`[]`) gelir.

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

**Örnek yanıt** (gerçek, kısaltılmış — 10 modülün ilk 4'ü, 97 content'ten
2'si: biri sıradan bir Text+Table içeriği, diğeri Image blokları ve
`variantGroupKey`/`variantLabel` içeren bir varyant örneği):

```json
{
  "version": 16,
  "book": {
    "id": 1,
    "title": "Kentsel Arama Kurtarma El Kitabı",
    "slug": "kentsel-arama-kurtarma-el-kitabi",
    "description": "Kentsel arama kurtarma operasyonlarında görev alan ekipler için temel başvuru kaynağı.",
    "languageCode": "tr",
    "version": 16
  },
  "modules": [
    {
      "id": 1,
      "bookId": 1,
      "name": "BSAFE",
      "description": "Sahada kisisel guvenlik icin temel kurallar ve senaryo bazli davranis rehberi.",
      "displayOrder": 0
    },
    {
      "id": 2,
      "bookId": 1,
      "name": "Olay Yönetimi",
      "description": "Olay yerinde sinyalizasyon, güvenlik protokolleri ve operasyonel yönetim esasları.",
      "displayOrder": 1
    },
    {
      "id": 3,
      "bookId": 1,
      "name": "Yapı Değerlendirmesi",
      "description": "Alan sektörleme, bölge tanımlama, INSARAG işaretleme sistemleri, kroki haritalar, hazmat ve yapısal değerlendirme esaslari.",
      "displayOrder": 2
    },
    {
      "id": 4,
      "bookId": 1,
      "name": "Arama Operasyonları",
      "description": "Arama guvenlik/operasyonel hususlari, ASR seviyeleri, kopekle arama ve gorusme teknikleri.",
      "displayOrder": 3
    }
  ],
  "contents": [
    {
      "id": 2,
      "moduleId": 2,
      "title": "Sinyaller ve Uyarılar",
      "summary": "Düdük, korna ve telsizle verilen standart olay yeri sinyalleri ile ışık sopası renk kodları.",
      "displayOrder": 0,
      "blocks": [
        {
          "id": 15,
          "type": 6,
          "text": null,
          "dataJson": "{\"rows\": [[\"İşi Durdurma / Herkesi Susturma\", \"1 Uzun Sinyal (3 saniye)\"], [\"Acil Alan Tahliyesi\", \"3 Kısa Sinyal (1 saniye)\"], [\"İşine Devam Edebilirsin\", \"1 Uzun 1 Kısa Sinyal\"]], \"headers\": [\"İşaret\", \"Sinyal\"]}",
          "media": null,
          "displayOrder": 0
        },
        {
          "id": 16,
          "type": 1,
          "text": "Düdük, havalı korna veya telsiz ile verilir.\n- Tahliye sonrasında, alanı tahliye eden tüm personel için telsiz anonsu geçin.\n- Alanı boşaltan tüm yetkili personel telsizden \"Tahliye sağlandı\" anonsu geçmeli, teyit alınmalı.",
          "dataJson": null,
          "media": null,
          "displayOrder": 1
        }
      ],
      "variantGroupKey": null,
      "variantLabel": null
    },
    {
      "id": 25,
      "moduleId": 5,
      "title": "Temel Düğümler — 8 Şeklinde Döngü Düğüm",
      "summary": "Kurtarma halatlarinda en sik kullanilan temel dugumlerden biri.",
      "displayOrder": 5,
      "blocks": [
        {
          "id": 89,
          "type": 1,
          "text": "Yaklaşık mukavemeti halatın %75'i kadardır. Kuyruğu en az 20cm uzunluğunda tutun. Her iki uç da yüklenebilir. Her türlü halat ve dokuma halat ile yapılabilir.",
          "dataJson": null,
          "media": null,
          "displayOrder": 0
        },
        {
          "id": 90,
          "type": 2,
          "text": null,
          "dataJson": null,
          "media": {
            "id": 13,
            "url": "media/kentsel-arama-kurtarma-el-kitabi/figur8-dugum.png",
            "checksum": "72209E6CB6E7FF6BA83E3216BC443F5BA60930527A1504F4D35347FD0B9B3BEF",
            "size": 48522
          },
          "displayOrder": 1
        }
      ],
      "variantGroupKey": "temel-dugumler",
      "variantLabel": "F8"
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
| `contents` | array | `id`, `moduleId`, `title`, `summary`, `displayOrder`, `blocks[]`, `variantGroupKey`, `variantLabel` |
| `contents[].blocks` | array | `id`, `type` (§4), `text`, `dataJson`, `media`, `displayOrder` |

**Notlar:**
- `contents` içindeki her blok, `media` alanı doluysa `MediaSummaryDto`
  şeklinde bir obje taşır (`id`, `url`, `checksum`, `size`); boşsa `null`.
- Silinmiş content'ler bu listede **hiç yer almaz** — snapshot her zaman
  o anki hayatta-olan durumu temsil eder.
- **`variantGroupKey` / `variantLabel` (v1.1, additive):** Çoğu content'te
  ikisi de `null` — modülün "Konular" listesinde tekil bir satır olarak
  gösterilir (örn. yukarıdaki "Sinyaller ve Uyarılar"). Bir konunun birden
  fazla varyantı varsa (yukarıdaki örnekte "Temel Düğümler" — F8/F9/TH/ABK
  dört ayrı düğüm türü), o varyantların hepsi **aynı** `variantGroupKey`
  değerini taşır (`"temel-dugumler"`) ve mobil bunları **tek bir sekmeli
  sayfada** birleştirmelidir — her sekmenin etiketi kendi `variantLabel`'ı
  (`"F8"`, `"F9"`, `"TH"`, `"ABK"`), sekme sırası kendi `displayOrder`'ıdır.
  Gruplama string ayrıştırmayla (başlıktan) **değil**, bu iki alanla
  yapılmalı; `title` sadece görüntü amaçlıdır ve değişebilir.
- **Üst listede (modülün "Konular" listesi) hangi title/summary gösterilir?**
  Aynı `variantGroupKey`'i paylaşan varyantlar arasında, **en küçük
  `displayOrder`'a sahip varyantın** `title`/`summary`'si o grubun tek
  satırını temsil eder. Ayrı bir `variantGroupTitle` alanı YOKTUR — mobil
  bunu, grubu zaten `displayOrder`'a göre sıraladığı için ek bir alan
  gerekmeden türetebilir.

### 3.3 `GET /sync/changes?bookId={id}&fromVersion={n}`

> **Bu endpoint bir GÜNLÜKTÜR, tam kopya DEĞİLDİR.** Yalnızca
> `fromVersion`'dan sonra **gerçekten değişmiş** (eklenmiş, düzenlenmiş
> veya silinmiş) content'ler döner. Bir content son yayından beri hiç
> değişmediyse, bu yanıtta **hiç yer almaz** — bu bir hata değil, tasarımın
> ta kendisi. "Neden bazı content'ler hiç gelmiyor?" sorusunun cevabı budur.

**Örnek yanıt** (gerçek, `fromVersion=17` — bir önceki yayın ile şu anki
(v18) arasındaki gerçek fark; bu yayında sadece bir content silinmiş,
yeni/değişen content yok):

```json
{
  "fromVersion": 17,
  "toVersion": 18,
  "book": {
    "id": 1,
    "title": "Kentsel Arama Kurtarma El Kitabı",
    "slug": "kentsel-arama-kurtarma-el-kitabi",
    "description": "Kentsel arama kurtarma operasyonlarında görev alan ekipler için temel başvuru kaynağı.",
    "languageCode": "tr",
    "version": 18
  },
  "upsertedContents": [],
  "deletedContentIds": [100],
  "modules": [
    { "id": 1, "bookId": 1, "name": "BSAFE", "description": "Sahada kisisel guvenlik icin temel kurallar ve senaryo bazli davranis rehberi.", "displayOrder": 0 },
    { "id": 2, "bookId": 1, "name": "Olay Yönetimi", "description": "Olay yerinde sinyalizasyon, güvenlik protokolleri ve operasyonel yönetim esasları.", "displayOrder": 1 },
    { "id": 3, "bookId": 1, "name": "Yapı Değerlendirmesi", "description": "Alan sektörleme, bölge tanımlama, INSARAG işaretleme sistemleri, kroki haritalar, hazmat ve yapısal değerlendirme esaslari.", "displayOrder": 2 },
    { "id": 4, "bookId": 1, "name": "Arama Operasyonları", "description": "Arama guvenlik/operasyonel hususlari, ASR seviyeleri, kopekle arama ve gorusme teknikleri.", "displayOrder": 3 },
    { "id": 5, "bookId": 1, "name": "Kaldırma & Taşıma", "description": "Kaldirma acilari, aski faktorleri, temel dugumler, vinc el sinyalleri ve kaldirac sistemleri.", "displayOrder": 4 },
    { "id": 6, "bookId": 1, "name": "Destekleme", "description": "Destekleme guvenligi, kereste secimi, Paratech destekleme sistemi ve ahsap tahkimat teknikleri.", "displayOrder": 5 },
    { "id": 7, "bookId": 1, "name": "Dehliz Açma & Kırma", "description": "Beton dehliz acma teknikleri ve sicak kesme operasyonlarinin guvenlik hususlari ve yontemleri.", "displayOrder": 6 },
    { "id": 8, "bookId": 1, "name": "Kapalı Alana Girme", "description": "Kapali alanlarda karsilasilan tehlikeler ve guvenli calisma sistemi adimlari.", "displayOrder": 7 },
    { "id": 10, "bookId": 1, "name": "Yaralı Bakımı", "description": "Kazazede değerlendirmesi, temel yaşam desteği ve saha ilk yardim protokolleri.", "displayOrder": 8 },
    { "id": 11, "bookId": 1, "name": "Referans", "description": "Malzeme yoğunlukları, kütle hesaplama formülleri ve birim dönüşüm tabloları.", "displayOrder": 9 }
  ],
  "addedMedia": [],
  "removedMediaIds": [94]
}
```

> Not: `modules` dizisindeki id'ler (1-8, 10, 11) **bilerek** ardışık değil
> — bir modül erken geliştirme sırasında oluşturulup geri alınmış, id'si
> hiç kullanılmadı (§8'deki "modül id'leri ardışık olmayabilir" notuyla
> tutarlı). Sıralama için her zaman `displayOrder`'a güvenin, `id`'ye
> değil.

**Alan tablosu:**

| Alan | Tip | Açıklama |
|---|---|---|
| `fromVersion` | int | İsteğinizdeki değer (yankı) |
| `toVersion` | int | Sunucunun güncel sürümü — sonraki `changes` isteğinizde bunu `fromVersion` olarak kullanın |
| `book` | object | **v1.2, additive.** `ToVersion`'daki güncel kitap durumu (`snapshot.book` ile aynı şekil) — `modules` gibi koşulsuz her yanıtta gelir, değişip değişmediğine bakılmaksızın |
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
| 5 | Warning | dolu | genelde boş | boş |
| 6 | Table | boş (`null`) | dolu (satır/sütun verisi) | boş |

**Gerçek Image örneği** (`type=2`, §3.2'deki "Temel Düğümler — 8 Şeklinde
Döngü Düğüm" content'inden):

```json
{
  "id": 90,
  "type": 2,
  "text": null,
  "dataJson": null,
  "media": {
    "id": 13,
    "url": "media/kentsel-arama-kurtarma-el-kitabi/figur8-dugum.png",
    "checksum": "72209E6CB6E7FF6BA83E3216BC443F5BA60930527A1504F4D35347FD0B9B3BEF",
    "size": 48522
  },
  "displayOrder": 1
}
```

**Gerçek Warning örneği** (`type=5`, "Hasta Sorgulama (SAMPLE)"
content'inden — bu kitapta Warning blokları `dataJson` kullanmıyor, sadece
`text` taşıyor; `dataJson` alanı boşsa da geçerlidir):

```json
{
  "id": 267,
  "type": 5,
  "text": "Yaralı veya şokta bir kişinin yanıtlarının doğru olduğunu varsaymayın.",
  "dataJson": null,
  "media": null,
  "displayOrder": 1
}
```

**Gerçek Table örneği** (`type=6`, "Sinyaller ve Uyarılar" content'inden):

```json
{
  "id": 15,
  "type": 6,
  "text": null,
  "dataJson": "{\"rows\": [[\"İşi Durdurma / Herkesi Susturma\", \"1 Uzun Sinyal (3 saniye)\"], [\"Acil Alan Tahliyesi\", \"3 Kısa Sinyal (1 saniye)\"], [\"İşine Devam Edebilirsin\", \"1 Uzun 1 Kısa Sinyal\"]], \"headers\": [\"İşaret\", \"Sinyal\"]}",
  "media": null,
  "displayOrder": 0
}
```

> `dataJson` her zaman bir **string**'dir (JSON içinde JSON) — kendi
> içeriğini ayrıca `JSON.parse` etmeniz gerekir, otomatik açılmaz.

Kitapta şu an `Video`/`Animation` tipinde blok yok (`Image` ve `Table` için
yukarıdaki gibi gerçek örnekler artık mevcut); bu iki tip için `media`/
`dataJson` alanı dolu geldiğinde şekli yukarıdaki §3.1'deki
`MediaSummaryDto` iledir.

> **⚠️ PROVISIONAL — henüz gerçek içerik yok, şema kesin DEĞİL (Faz 13.7).**
> Aşağıdaki iki alt bölüm, mobil ekibin UI'ı önceden tasarlayabilmesi için
> bir başlangıç noktası — §5'teki checksum kuralı gibi **donmuş bir
> sözleşme değil**. Kitaba gerçek bir Video/Animation bloğu eklendiğinde bu
> bölüm gerçek bir örnekle değiştirilecek (tıpkı Image/Table'ın yukarıda
> yapıldığı gibi) ve o noktada şekil değişebilir.

### 4.1 Video (`type=3`) — provisional

Video dosyasının kendisi zaten `media` alanında (`MediaSummaryDto`) taşınıyor
— ayrı bir `dataJson` şemasına **gerek yok** varsayılan durumda, `dataJson`
`null` kalır. Tek olası ek alan: video dosyasından ayrı, elle seçilmiş bir
kapak görseli isteniyorsa (`media`'daki dosyanın ilk karesi otomatik
üretilmiyor, backend'de video transcoding/thumbnail çıkarma yok):

```json
{
  "type": 3,
  "text": null,
  "dataJson": "{\"thumbnailMediaId\":42}",
  "media": { "id": 41, "url": "media/.../egzersiz-videosu.mp4", "checksum": "...", "size": 12345678 }
}
```

`thumbnailMediaId`, ayrı bir `Media` satırına (bir `Image` dosyasına) işaret
eder — mobil bunu `media[]` listesinde `id`'ye göre arayıp çözer, aynı
manifest'teki `media` dizisi gibi (bkz. §3.1). Gerçek bir video içeriği
gelene kadar bu alan hiç kullanılmayabilir de.

### 4.2 Animation (`type=4`) — provisional

Bir animasyon genelde **birden fazla** görsel/adımdan oluşur (örn. bir
düğüm bağlama sekansı) — tek bir `media` alanı yetmez, bu yüzden
`dataJson` içinde bir `steps` dizisi öneriliyor, her adım kendi kısa
metnini ve (varsa) kendi görselinin `Media` id'sini taşır:

```json
{
  "type": 4,
  "text": null,
  "dataJson": "{\"steps\":[{\"text\":\"İpin ucunu çapraz geçirin.\",\"mediaId\":50},{\"text\":\"Halkadan geçirip sıkın.\",\"mediaId\":51}]}",
  "media": null
}
```

`media` alanı bu tipte muhtemelen hep `null` kalır — her adımın kendi
`mediaId`'si `dataJson` içinde taşınır (tek bir "kapak" görseli lazımsa
o da `media` alanına konabilir, ama bu henüz gerçek bir ihtiyaçla
doğrulanmadı — YAGNI, ilk gerçek Animation içeriği eklendiğinde netleşir).
Adım süresi/otomatik geçiş hızı gibi zamanlama bilgisi bilerek dışarıda
bırakıldı — hiçbir gerçek kullanım örneği bunu gerektirmiyor henüz.

---

## 5. Bütünlük Doğrulama (Checksum)

İndirilen `snapshot`'ın bozulmadığından emin olmak için:

1. `GET /sync/manifest?bookId={id}` çekin, `checksum` alanını saklayın.
2. `GET /sync/snapshot?bookId={id}` çekin — yanıtı **ham baytlar** olarak
   alın, henüz JSON'a parse ETMEDEN.
3. O ham baytların SHA-256'sını hesaplayın, hex string'e çevirin.
4. Hex string'i **büyük harfe çevirip** (`ToUpperInvariant()` benzeri)
   manifest'teki `checksum` ile karşılaştırın — sunucu büyük harfli hex
   üretir (örn. `DD8076ED...`, `dd8076ed...` değil).
5. Tutmuyorsa: indirme bozulmuş demektir, yeniden indirin. Tutuyorsa:
   snapshot'ı JSON'a parse edip local veritabanına yazabilirsiniz.

**Bu tarifin gerçekten çalıştığının kanıtı** (2026-08-28 tarihinde,
gerçek `snapshot` v16 yanıtı üzerinde elle doğrulandı):

```
SHA256(snapshot ham baytları) = DD8076ED4D90BA850535F9987D7C7F20F4FEC2AA32C847A324F276C79EE920B5
manifest.checksum              = DD8076ED4D90BA850535F9987D7C7F20F4FEC2AA32C847A324F276C79EE920B5
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
| `Sync.BookNotFound` | 404 | Bu id'de bir kitap hiç yok (yanlış id / konfigürasyon hatası — `bookId` query parametresi eksik/sayısal değilse de int'e çevrilemediği için `0`'a düşer ve bu hatayı üretir) | Üçü de |
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
| Modül/content id'leri ardışık olmayabilir | Geliştirme sırasında oluşturulup silinen kayıtlar id boşluğu bırakır (§3.3'teki gerçek örnekte modül id'leri 1-8, 10, 11 — 9 yok) | Sıralama için her zaman `displayOrder`'a güvenin, id'ye değil |

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
| v1.1 | 2026-08-26 | Additive: `contents[]`'e `variantGroupKey`/`variantLabel` eklendi — çok-varyantlı konuların (düğüm türleri gibi) mobilde string ayrıştırma yapılmadan sekmeli tek sayfada birleştirilebilmesi için (§3.2) |
| — | 2026-08-28 | **Sözleşme değişmedi** (hâlâ v1.1) — tüm örnekler, kitabın tamamı yayınlandıktan sonraki gerçek v16 API yanıtlarıyla yenilendi (önceki örnekler 4 modüllük eski yer tutucu veriye aitti). İlk kez gerçek bir Image blok örneği (§3.2, §4) ve gerçek bir `changes` deltası (§3.3) eklendi. |
| v1.2 | 2026-08-29 | Additive: `changes`'e `book` alanı eklendi (§3.3) — `manifest`/`snapshot`'ın aksine kitabın kendi meta verisini hiç taşımıyordu, `modules`'la aynı gerekçeyle (Faz 13.2, mobil ekip geri bildirimi #4) koşulsuz eklendi. §2'ye medya base URL netleştirmesi, §3.2'ye varyant grubu üst-liste title/summary kuralı eklendi (mobil ekip geri bildirimi #1, #2) — ikisi de mevcut davranışın dokümantasyonu, sözleşme değişikliği değil. |
