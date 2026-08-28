# Web Frontend (arama-kurtarma-web) — Geri Bildirim ve Entegrasyon Bulguları

`https://github.com/Isbak-SARGuide/arama-kurtarma-web` reposunun `main` dalı (son commit:
`ecfe731`, "Frontend Final: API mock izolasyonu ve Backend Geliştirici README entegrasyonu
eklendi") üzerinde yapılan incelemenin sonucu. Amaç: backend Faz 5 (CMS Completion) ve
sonrasında bu frontend'i gerçek API'ye bağlarken karşılaşılacak pürüzleri önceden görmek.

**Bu doküman backend reposunda yaşıyor** (web reposunda değil) çünkü bulguların çoğu iki
tarafın sözleşme uyumuyla ilgili — `docs/Sync-Sozlesmesi-v1.md` ile karşılaştırmalı okunmalı.

**Durum:** İnceleme tamamlandı, hiçbir web-repo dosyası değiştirilmedi (salt okunur inceleme).

---

## Genel Değerlendirme

Frontend, backend hiç var olmadan, kendi kurgusal "mock" veri modeliyle **izole** geliştirilmiş
(README kendi ifadesiyle: "izole bir şekilde Mock Data ile test edilmiştir"). Görsel/UI katmanı
(React + Tailwind, admin layout, kullanıcı okuyucu ekranı) olgun görünüyor, ama **veri modeli
backend'in gerçek şemasıyla temelden uyuşmuyor**. README'deki "Backend hazır olduğunda sadece
`api.js`'teki simülasyonları kaldırmanız yeterli" iddiası **yanıltıcı** — bu, bir satır değişikliği
değil, okuma katmanının yeniden yazılması anlamına geliyor (bkz. Bulgu 1).

| Öncelik | Sayı |
|---|---|
| 🔴 CRITICAL | 2 |
| 🟠 HIGH | 3 |
| 🟡 MEDIUM | 3 |
| 🟢 LOW | 1 |

---

## 🔴 CRITICAL

### 1. İçerik veri modeli backend şemasıyla temelden uyuşmuyor

**Nerede:** `src/data/mock.js`, `src/services/api.js`, `src/utils/markdownParser.js`,
`README.md` §"Beklenen API Endpoint'leri"

Frontend'in varsaydığı model **düz**: `chapter { sections: [{ content: "# markdown string" }] }`
— tek bir Markdown metin bloğu. Backend'in gerçek, donmuş modeli ise **tipli blok dizisi**
(`Sync-Sozlesmesi-v1.md` §3.2, §4):

```
Content { blocks: [
  { type: 1 (Text), text: "..." },
  { type: 5 (Warning), text: "...", dataJson: "{\"severity\":\"high\"}" },
  { type: 6 (Table), dataJson: "{\"headers\":[...], \"rows\":[[...]]}" },
  { type: 2 (Image), media: { url, checksum, size } },
  ...
]}
```

Bunlar birbirine **çevrilemez** kayıpsız şekilde — özellikle Table (`type=6`) blokları. Az önce
backend'e giren gerçek kitaptan örnek: Glasgow Koma Skalası tablosu, malzeme yoğunluk tablosu
(21 satır) gibi — bunlar yapılandırılmış satır/sütun verisi, Markdown'da "tablo" olarak
gösterilebilir ama `markdownParser.js`'de **tablo desteği hiç yok** (bkz. Bulgu 2).

**Sonuç:** README'nin önerdiği "mock'u kaldır, fetch ekle" entegrasyonu çalışmaz. Gerekli olan:
- Okuma tarafı (`HandbookReader.jsx`, `ContentEditor.jsx` önizlemesi) `parseMarkdown(text)`
  yerine `blocks.map(block => renderBlock(block))` şeklinde **tip bazlı render**'a geçmeli
  (backend'in `ContentBlockType` enum'una birebir: Text=1, Image=2, Video=3, Animation=4,
  Warning=5, Table=6 — `docs/Sync-Sozlesmesi-v1.md` §4).
- Admin editörü de tek bir `<textarea>` yerine **blok listesi editörü**'ne dönüşmeli (metin
  bloğu ekle, görsel bloğu ekle, tablo bloğu ekle, vb.) — bu, ContentEditor'ün mevcut
  "toolbar + textarea + canlı önizleme" tasarımının kavramsal olarak değişmesi demek.

**Öneri:** Bu, Faz 5 (backend CMS endpoint'leri: `/api/v1/modules`, `/api/v1/contents`,
`/api/v1/contentblocks`) bittikten hemen sonra, entegrasyona başlamadan önce web ekibiyle
netleştirilmesi gereken bir **mimari karar** — küçük bir "sözleşme hizalama" toplantısı/dokümanı
gerekir, doğrudan koda dalmak riskli.

### 2. Beklenen API sözleşmesi gerçek backend'le uyuşmuyor

**Nerede:** `README.md` §"Beklenen API Endpoint'leri ve Veri Formatları"

Web reposunun README'si şu endpoint'leri "beklediğini" belgeliyor:
- `GET /api/handbook/chapters`
- `GET /api/handbook/chapters/{chapterId}/sections/{sectionId}`
- `POST /api/uploads`

Gerçek backend sözleşmesi (donmuş, `docs/Sync-Sozlesmesi-v1.md`) tamamen farklı bir modelde:
- `GET /api/v1/sync/manifest?bookId={id}` / `snapshot` / `changes?fromVersion={n}` (anonim,
  mobil/web okuyucu için — versiyonlu, delta tabanlı)
- Admin CMS tarafı ayrı: `GET/POST/PUT/DELETE /api/v1/books` (Faz 5 ile birlikte
  `/api/v1/modules`, `/api/v1/contents`, `/api/v1/contentblocks` eklenecek), JWT ile korumalı.

Ayrıca web README'sindeki `targetPlatform: "all"|"web"|"mobile"` alanı backend şemasında **hiç
yok** — `Content` entity'sinde böyle bir ayrım yapılmıyor (CLAUDE.md: "web okuyucu, mobil gibi
aynı immutable yayın tablolarından beslenen ikinci bir tüketicidir" — platform bazlı içerik
filtreleme değil, aynı içerik farklı istemcilerde gösteriliyor).

**Öneri:** README'nin "Backend Geliştiricisi İçin Devir Rehberi" bölümü, gerçek `manifest`/
`snapshot`/`changes` + CMS endpoint'leriyle **yeniden yazılmalı**; `targetPlatform` fikri terk
edilmeli (backend'de karşılığı yok, ihtiyaç gerçekse ayrı bir konu olarak ele alınmalı).

---

## 🟠 HIGH

### 3. `markdownParser.js` ham HTML'i doğrudan geçiriyor — XSS riski

**Nerede:** `src/utils/markdownParser.js:17-20`, kullanım noktaları
`HandbookReader.jsx:137` ve `ContentEditor.jsx:245,261` (`dangerouslySetInnerHTML`)

```js
if (block.startsWith('<')) {
  return block;  // ham HTML aynen geçiyor, hiçbir temizlik yok
}
```

Şu an içerik sadece geliştirici tarafından yazılan mock veri olduğu için sorun görünmüyor, ama
`ContentEditor.jsx:92`'de görsel ekleme özelliği zaten kullanıcı girdisinden **doğrudan ham HTML
üretip** Markdown kaynağına enjekte ediyor (`insertText('<img src="${url}" ...' )`). İçerik gerçek
bir admin panelinden (birden fazla Editor rolü kullanıcısı, CLAUDE.md'deki `RoleNames.Editor`)
geliyor olacağı için, bu satır + yukarıdaki "ham HTML'i aynen geçir" davranışı birleşince
**stored XSS** için hazır bir yapı oluşuyor: bir Editor kullanıcısı `<img onerror="...">` gibi bir
payload yazıp kaydederse, bunu okuyan her kullanıcının tarayıcısında çalışır.

**Öneri:** İçerik modeli Bulgu 1'e göre tipli bloklara geçtiğinde bu risk zaten büyük ölçüde
ortadan kalkar (serbest Markdown yerine yapılandırılmış `text`/`dataJson` alanları, HTML enjekte
edilecek bir yüzey kalmaz). Geçiş tamamlanana kadar, en azından bir sanitizer (`DOMPurify` gibi)
`dangerouslySetInnerHTML`'den önce araya konmalı — bu web/security.md kuralındaki "Escape dynamic
template values / sanitize edilmemiş HTML enjekte etme" maddesiyle doğrudan çakışıyor.

### 4. Admin girişi tamamen sahte ve trivially bypass edilebilir

**Nerede:** `src/pages/admin/Login.jsx:22`, `src/components/layout/ProtectedRoute.jsx:5`

```js
if (email === 'admin@akut.org' && password === '123456') {
  localStorage.setItem('isAdminLoggedIn', 'true');
```
```js
const isAuthenticated = localStorage.getItem('isAdminLoggedIn') === 'true';
```

Kimlik bilgileri hem koda hem ekrana ("Demo Bilgileri: ...") gömülü; koruma sadece
`localStorage`'da bir string kontrolü — tarayıcı konsolundan
`localStorage.setItem('isAdminLoggedIn','true')` yazan **herkes** admin paneline girer, şifre
bile gerekmez. Backend'de gerçek JWT + rol tabanlı auth zaten hazır
(`AuthController`, `RoleNames.Admin/Editor`, Faz 9'da refresh token gelecek).

**Öneri:** Entegrasyon sırasında bu tamamen atılıp gerçek `POST /api/v1/auth/login` + JWT
saklama (mem/httpOnly cookie tercih edilir, `localStorage`'da ham token saklamak XSS riskini
büyütür — bkz. Bulgu 3 ile birleşince özellikle tehlikeli) + `Authorization: Bearer` header'ıyla
değiştirilmeli. "Mock'u kaldır" kadar basit değil, gerçek bir auth akışı kurulumu.

### 5. `ContentEditor` kaydetme işlemi hiçbir şeyi kalıcı hale getirmiyor

**Nerede:** `src/pages/admin/ContentEditor.jsx:75-83`

```js
const handleSave = (e) => {
  e.preventDefault();
  setIsSaving(true);
  // Simüle edilmiş kayıt işlemi
  setTimeout(() => {
    setIsSaving(false);
    setIsDirty(false);
  }, 1000);
};
```

Bu sadece bir spinner gösterip 1 saniye sonra "kaydedildi" izlenimi veriyor — `api.js`'e (mock
katmanına bile) hiç dokunmuyor. `api.js`'te zaten create/update için bir mock fonksiyon da yok
(sadece `fetchHandbookChapters`, `fetchChapterById`, `fetchSectionContent`, `uploadImage` var).
Yani editördeki her değişiklik sayfa yenilendiğinde sessizce kayboluyor — bu backend
entegrasyonundan bağımsız, mock aşamasının kendi içinde bile eksik.

**Öneri:** Gerçek entegrasyonda `handleSave`, Faz 5'in `POST/PUT /api/v1/contents/{id}` ve
`POST/PUT /api/v1/contentblocks` çağrılarına bağlanmalı; ama bloklara geçiş (Bulgu 1) önce
netleşmeli, yoksa burada yazılacak entegrasyon kodu bir sonraki değişiklikte atılacak.

---

## 🟡 MEDIUM

### 6. Kullanılmayan, yarım kalmış "gerçek kitap" verisi — kafa karıştırıcı ölü kod

**Nerede:** `src/data/handbook.js` + `src/data/chapters/*.js` (6 dosya)

`handbook.js` ve altındaki `chapters/` klasörü, gerçek el kitabının modüllerine çok yakın
isimlerle (`bsafe`, `destekleme`, `kaldirma-tasima`, `dehliz-acma`, `kapali-alan`,
`yarali-bakimi`, `referans`) ve BSAFE için gerçek kitap metnine neredeyse birebir içerikle
hazırlanmış — ama **hiçbir yerden import edilmiyor**. Uygulama gerçekte `src/data/mock.js`'i
kullanıyor (`api.js:1`), o da tamamen farklı, kurgusal bir "deprem hazırlığı" içerik seti
(8 makale: Acil Durum Seti, Tehlike Avı, Triyaj, ...) — gerçek USAR el kitabıyla **hiç
örtüşmüyor**.

Yani repoda aynı anda üç farklı, birbiriyle örtüşmeyen içerik kaynağı var: (1) çalışan `mock.js`,
(2) ölü `handbook.js`/`chapters/*`, (3) backend'deki gerçek 97 konu. Bu, yeni katılan birinin
"hangisi doğru?" diye kaybolmasına yol açar.

**Öneri:** `handbook.js`/`chapters/*` ya silinmeli (gerçek içerik zaten backend'de, tekrar
transkript gerekmiyor) ya da en azından bir yorum/README notuyla "kullanılmıyor, referans
amaçlı" diye işaretlenmeli. Backend entegrasyonu sonrası ikisi de zaten gereksiz kalacak.

### 7. Docker imajı production'da dev server çalıştırıyor

**Nerede:** `Dockerfile:5`

```dockerfile
CMD ["npm", "run", "dev"]
```

`npm run dev` Vite'ın geliştirme sunucusu — HMR websocket'i, minify edilmemiş bundle, dev-only
uyarılar içerir. Prod imajı `vite build` çıktısını statik bir sunucudan (nginx, `vite preview`
değil) servis etmeli. Şu an bu muhtemelen sorun değil çünkü entegrasyon henüz başlamadı, ama
backend'in Faz 11 (Release Readiness — Dockerfile, prod compose) ile hizalanacağı noktada web
tarafının da aynı standarda gelmesi gerekecek.

### 8. Ortam bazlı API URL yapılandırması yok

**Nerede:** repo genelinde — `.env`/`import.meta.env` kullanımı yok

`api.js` hiç network çağrısı yapmadığı için şu an sorun değil, ama gerçek entegrasyonda dev/
staging/prod backend URL'lerini ayırmak için Vite'ın `import.meta.env.VITE_API_BASE_URL` gibi bir
mekanizma baştan kurulmalı — sonradan eklemek, her `fetch` çağrısını tekrar dokunmayı gerektirir.

---

## 🟢 LOW

### 9. `uploadImage` gerçek bir yükleme yapmıyor, `Blob` URL'i döndürüyor

**Nerede:** `src/services/api.js:70-80`

`URL.createObjectURL(file)` sadece tarayıcı belleğinde geçici bir referans oluşturur — sayfa
yenilenince kaybolur, hiçbir yere kalıcı yazılmaz. Bulgu 1/5 çözülene kadar bunun düzeltilmesi
anlamlı değil (zaten Faz 6 Media Pipeline bitmeden gerçek bir hedef yok), ama entegrasyon
sırasının bir parçası olarak not düşülüyor: backend'in gerçek `MediaController` (Faz 10) hazır
olunca buraya bağlanacak.

---

## Önerilen Entegrasyon Sırası

Bulgular birbirine bağımlı olduğu için önerilen sıra:

1. **Backend Faz 5 (CMS Completion) bitsin** — Module/Content/ContentBlock CRUD endpoint'leri
   canlı olmadan web tarafı gerçek veri yazamaz.
2. **Blok tabanlı render'a geçiş (Bulgu 1)** — önce okuma tarafı (`HandbookReader.jsx`), sonra
   editör (`ContentEditor.jsx`). Bu, web ekibiyle üzerinde konuşulması gereken en büyük mimari
   değişiklik; erken haber verilmeli.
3. **Gerçek auth (Bulgu 4)** — Faz 9 (refresh token) beklenmeden mevcut `AuthController` login'i
   ile bile başlanabilir, JWT + `Authorization` header yeterli.
4. **`handleSave`'i gerçek CRUD'a bağla (Bulgu 5)**, ölü veri dosyalarını temizle (Bulgu 6).
5. **Media pipeline (Faz 10) bitince** `uploadImage`'ı gerçek endpoint'e bağla (Bulgu 9).
6. **Release öncesi:** Dockerfile'ı prod build'e çevir (Bulgu 7), env bazlı API URL ekle (Bulgu 8),
   XSS/sanitizasyon taramasını (Bulgu 3) `security-reviewer` ile son bir kez doğrula.

Bu doküman web reposuna **gönderilmedi** (salt okunur inceleme) — web ekibiyle paylaşılacaksa bir
issue/PR yorumu olarak aktarılması gerekir.
