# Web Frontend (arama-kurtarma-web) — Geri Bildirim Round 2 (Faz 5 Entegrasyon Doğrulaması)

`https://github.com/Isbak-SARGuide/arama-kurtarma-web` reposunun `main` dalı (son commit:
`dc7d1bc`, "fix: Backend Faz 5 tasima katmani (Round 2) duzeltmeleri") üzerinde yapılan
incelemenin sonucu. Bu doküman round 1 ile aynı taşıma-katmanı bulgularını (`23ce0f8` üzerinde)
tespit etmiş, web ekibi `dc7d1bc` ile buna yanıt vermişti; bu güncelleme **o düzeltme commit'inin
kendisini**, geçerli backend kaynak koduna (ModulesController/ContentsController/
ContentBlocksController/MediaController + DTO'lar, hepsi doğrudan okunarak) karşı yeniden
doğrular.

**Durum:** İnceleme tamamlandı, hiçbir web-repo dosyası değiştirilmedi (salt okunur inceleme).

**Sonuç özeti:** `dc7d1bc` gerçek ilerleme kaydetti — auth, Content/ContentBlock kaydının
gövde şekli ve XSS bulgusu **gerçekten** düzeldi (kaynak koddan doğrulandı, aşağıda ✅
işaretli). Ama **hiçbir ekran içerik listeleyemiyor** — admin panelinin modül dropdown'ı,
kategori listesi, dashboard ve genel okuyucunun kendisi dahil **7 farklı ekran**, tek bir
fonksiyonun (`fetchHandbookChapters`) yanlış endpoint'e yanlış parametreyle gittiği için boş
kalıyor. Bu, round 1/round 2'de yakalanmamış, kapsamı en geniş bulgu.

| Öncelik | Sayı |
|---|---|
| 🔴 CRITICAL | 3 |
| 🟠 HIGH | 1 |

---

## ✅ Round 2'de gerçekten düzelen (kaynak koddan doğrulandı)

- **Login (round 1 Bulgu 4 / round 2 Bulgu 3):** `loginAdmin` artık `{userName, password}`
  gönderiyor, `data.accessToken` okuyor — backend'in `LoginDto(UserName, Password)` ve
  `LoginResponseDto.AccessToken`'ıyla birebir eşleşiyor. Sahte mock fallback (`admin@akut.org`)
  tamamen kaldırılmış. **Gerçek çalışıyor.**
- **XSS (round 1 Bulgu 3):** `markdownParser.js` artık `DOMPurify.sanitize(rawHtml)` ile dönüyor
  (satır 1: `import DOMPurify from 'dompurify'`, satır 108: `return DOMPurify.sanitize(rawHtml)`),
  `package.json`'da `dompurify` bağımlılığı var. **Gerçek çalışıyor**, iddia değil.
- **Content/ContentBlock kayıt gövdesi (round 2 Bulgu 1):** `saveContent`'in payload'ı
  (`{title, summary, isPublished, variantGroupKey, variantLabel}`) backend'in
  `CreateContentDto`/`UpdateContentDto`'suyla alan alan eşleşiyor; `saveContentBlock`'un
  payload'ı (`{type, text, dataJson, mediaId}`) da `CreateContentBlockDto`'yla eşleşiyor.
  Route'lar da doğru: `POST/PUT /api/v1/modules/{moduleId}/contents[/{id}]` ve
  `POST/PUT /api/v1/contents/{contentId}/blocks[/{id}]` — backend'deki gerçek
  `ContentsController`/`ContentBlocksController` route tanımlarıyla birebir. **Bu kısım artık
  doğru** — ama aşağıdaki Bulgu 1 çözülmeden bir Content'i düzenlemek için gereken `moduleId`ye
  UI'dan hiç ulaşılamıyor, o yüzden pratikte hâlâ test edilemez durumda.
- **Medya yükleme route'u (round 1 Bulgu 4 — Faz 6 artık bitmiş):** `POST /api/v1/media`,
  `multipart/form-data`, `file` alan adı — backend'in gerçek (ve artık var olan)
  `MediaController.Upload(IFormFile? file, ...)` imzasıyla birebir uyuyor. Route/wiring doğru
  (bkz. Bulgu 4 — yanıt gövdesinin okunuşu ayrı bir sorun).

---

## 🔴 CRITICAL

### 1. `fetchHandbookChapters` yanlış endpoint'e gidiyor — 7 ekranın tamamı boş kalıyor

**Nerede:** `src/services/api.js:44-55` (`fetchHandbookChapters`, `fetchChapterById`)

**Kullanıldığı yerler (hepsi etkileniyor):** `UserLayout.jsx`, `ArticleList.jsx`,
`CategoryList.jsx`, `Dashboard.jsx`, `ContentEditor.jsx` (modül dropdown'ı), `HandbookReader.jsx`
(genel okuyucu, mobil değil web tarafı) — yani **hem admin panelinin hem public okuyucunun
tamamı**.

```js
export const fetchHandbookChapters = async (bookId = 'default') => {
  const response = await fetch(`${API_BASE_URL}/api/v1/sync/manifest?bookId=${bookId}`);
  ...
  return data.chapters || [];
};
```

İki bağımsız sebepten bu her zaman boş dizi döner (ya da hiç yanıt bile almaz):

1. **`bookId='default'` bir string, backend `int` bekliyor.** `SyncController.GetManifest([FromQuery] int bookId, ...)` — `[ApiController]` attribute'u olan bir controller'da tip dönüştürülemeyen bir query param, action hiç çalışmadan otomatik **400 Bad Request** üretir. Gerçek kitabın id'si `1` (tek kitap var), ama frontend'de hiçbir yerde bu gerçek id'ye ulaşan bir kod yok — hem `fetchHandbookChapters` hem `createModule`/`updateModule` aynı `'default'` yer tutucusunu kullanıyor.
2. **`manifest` endpoint'i zaten `chapters` alanı hiç döndürmez.** Gerçek yanıt şekli (`Sync-Sozlesmesi-v1.md` §3.1, canlı `curl` ile de doğrulandı): `{bookId, version, publishedAt, contentCount, media, checksum}`. `.chapters` alanı **hiçbir zaman** var olmadı — `data.chapters || []` her zaman `[]`'a düşer, sunucu 200 dönse bile.

**Daha derin sorun — bu bir "alan adı düzeltmesi" değil, mimari bir karışıklık:** Admin panelinin
modül/içerik listelerini doldurmak için mobil/web-okuyucu'ya özel, **anonim, yayınlanmış**
sync endpoint'i (`/sync/manifest`) kullanılıyor. Bu endpoint admin'in ihtiyacı olan şeyi
(taslak dahil tüm modüller/içerikler, gerçek DB id'leri, sayfalama) hiçbir zaman veremez —
tasarım gereği sadece **yayınlanmış** sürümü, versiyon bazlı gösterir. Admin panelinin
kullanması gereken, Faz 5'te tamamlanmış CMS liste uçları:
`GET /api/v1/books/{bookId}/modules` ve `GET /api/v1/modules/{moduleId}/contents`
(ikisi de `{items, totalCount, page, pageSize}` şeklinde sayfalı döner — `PagedResult<T>`).
Public okuyucu (`HandbookReader.jsx`) için `/sync/snapshot` doğru endpoint olurdu (o da
`chapters` değil `modules`+`contents` alanlarını ayrı diziler olarak taşır — aynı isim
uyuşmazlığı orada da var).

**Öneri:**
- Admin tarafı için `fetchHandbookChapters`'ı tamamen kaldırıp yerine `fetchModules(bookId)`
  (→ `GET /api/v1/books/{bookId}/modules`) ve `fetchContents(moduleId)`
  (→ `GET /api/v1/modules/{moduleId}/contents`) eklenmeli — ikisi de JWT gerektirir
  (`getAuthHeaders()`).
- Public okuyucu için ayrı bir `fetchPublishedSnapshot(bookId)` (→ `/sync/snapshot`) yazılmalı,
  yanıttaki `modules`/`contents` dizileri UI'nin beklediği `chapters[].sections[]` şekline
  frontend tarafında dönüştürülmeli (backend'in alan adlarını UI'nin beklediği isimlere zorlamak
  yerine).
- `bookId='default'` yer tutucusu her yerden kaldırılmalı — gerçek kitabın id'sini (şu an `1`)
  ya sabit bir konfigürasyon değeri olarak ya da `GET /api/v1/books` listesinden ilk kitabı
  alarak çözmek gerekiyor (UI'da hâlâ kitap seçimi yok, round 1 Bulgu 6/round 2 notlarında da
  işaretlenmişti).

### 2. `createModule`/`updateModule` de aynı `bookId='default'` hatasını taşıyor

**Nerede:** `src/pages/admin/CategoryEditor.jsx:38-40`

```js
// Geçerli bir bookId varsayıyoruz, çünkü UI'da book seçimi yok
const bookId = 'default';
```

`ModulesController`'ın route'u `api/v{version}/books/{bookId:int}/modules` — `:int` route
constraint'i, `'default'` gibi sayısal olmayan bir segment'e **hiç eşleşmez** (routing seviyesinde
404, action'a hiç girilmez; Bulgu 1'deki `[FromQuery]` durumundan farklı olarak burada 400 bile
alınmaz, sessiz bir 404 olur). Bu, round 2'nin kendi eklediği `createModule`/`updateModule`
fonksiyonlarının **hiçbirinin şu an çalışmadığı** anlamına geliyor — Bulgu 1'le aynı kök sebep
(gerçek `bookId` hiçbir yerde çözülmüyor), tek düzeltmeyle ikisi birden kapanır.

**Sonuç:** Admin panelinden yeni bir modül (BSAFE, Olay Yönetimi gibi ana bölüm) oluşturmak
şu an da mümkün değil — round 1'deki durumla aynı, sadece hata şekli değişti (sahte kaydetme →
gerçek ama her zaman başarısız istek).

**Öneri:** Bulgu 1'in çözümüyle birlikte gelir — gerçek `bookId` bir yerden çözülünce
(sabit `1` veya `GET /api/v1/books`'tan) bu iki fonksiyon zaten doğru route'u kullanıyor,
başka bir değişikliğe gerek yok.

### 3. `fetchSectionContent`, URL'de `moduleId`'yi hiç taşımıyor

**Nerede:** `src/services/api.js:69-80`

```js
export const fetchSectionContent = async (chapterId, sectionId) => {
  const response = await fetch(`${API_BASE_URL}/api/v1/contents/${sectionId}`);
  ...
};
```

`chapterId` parametre olarak alınıyor ama gövdede/URL'de **hiç kullanılmıyor**. Backend'in
gerçek route'u `api/v{version}/modules/{moduleId:int}/contents/{id:int}` — `moduleId` segment'i
olmadan bu istek her zaman 404 döner. `ContentEditor.jsx`'te bu fonksiyon var olan bir içeriği
düzenlemeye açarken çağrılıyor (`fullSection.blocks`'u yüklemek için) — yani düzenleme ekranı
hiçbir zaman gerçek blokları göremiyor.

**Öneri:** `fetchSectionContent(moduleId, contentId)` imzasına geçirip URL'i
`` `${API_BASE_URL}/api/v1/modules/${moduleId}/contents/${contentId}` `` yapmak yeterli —
çağıran taraf (`ContentEditor.jsx:50`) zaten `foundChapterId`'yi elinde tutuyor, sadece
parametre olarak geçmiyor.

---

## 🟠 HIGH

### 4. `uploadImage` yanıtı yanlış alan okuyor — upload sunucuda başarılı olsa bile frontend'de çöküyor

**Nerede:** `src/services/api.js:167-191` (`uploadImage`)

```js
const data = await response.json();
return {
  id: data.id,
  url: data.url.startsWith('http') ? data.url : `${API_BASE_URL}${data.url}`
};
```

Backend'in `MediaController.Upload` uç noktası başarıyla yanıt verse bile gövde
`MediaDto`'dur (`Id, FileName, StoragePath, MediaType, ContentType, FileSize, Checksum, Width,
Height, Duration, CreatedAt`) — **`Url` diye bir alan yok**, dosya yolu `storagePath`
(camelCase JSON: `storagePath`) olarak gelir. `data.url` her zaman `undefined` olacağı için
`data.url.startsWith(...)` satırı `TypeError: Cannot read properties of undefined` ile
patlar — resim sunucuya gerçekten yüklenmiş olsa bile (medya kaydı DB'de oluşur) frontend bunu
asla göremez, kullanıcıya hata gösterir.

**Öneri:** `data.url` yerine `data.storagePath` okunmalı; tam URL'e çevirmek için
`` `${API_BASE_URL}/${data.storagePath}` `` (mutlak yol değilse) kullanılmalı — backend'in
`Media.StoragePath` alanı zaten göreli bir depolama yolu tutuyor (bkz. CLAUDE.md "Medya
depolama"), `url` diye ayrı bir alan asla eklenmedi.

---

## Önerilen Sıra

1. **`bookId` çözümünü tek yerden hallet (Bulgu 1 + 2 birlikte)** — gerçek kitabın id'sini
   (`1`) bir sabit ya da `GET /api/v1/books`'tan çözen tek bir yardımcı yazıp
   `fetchHandbookChapters`/`createModule`/`updateModule`'un hepsinin kullandığı `'default'`
   yerine bunu geçir. Tek değişiklik, iki CRITICAL bulguyu birden kapatır.
2. **Admin liste fonksiyonlarını gerçek CMS uçlarına taşı (Bulgu 1'in devamı)** —
   `fetchModules`/`fetchContents` ekle, admin ekranlarını `/sync/manifest` yerine bunlara bağla.
3. **`fetchSectionContent`'e `moduleId` ekle (Bulgu 3)** — küçük ama düzenleme ekranını
   tamamen açan bir değişiklik.
4. **`uploadImage`'ı `storagePath`'e çevir (Bulgu 4)** — Faz 6 zaten bitmiş, sadece bu satır
   kaldı.
5. Bunlar bittikten sonra round 2'nin zaten doğru kurduğu Content/ContentBlock kayıt akışı
   (yukarıdaki ✅ bölümü) gerçek anlamda uçtan uca test edilebilir hale gelir.

Bu doküman web reposuna **gönderilmedi** (salt okunur inceleme) — web ekibiyle paylaşılacaksa
bir issue/PR yorumu olarak aktarılması gerekir.
