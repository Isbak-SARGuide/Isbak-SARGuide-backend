# Isbak SAR Guide — CMS API Sözleşmesi v1

Bu doküman, admin web panelinin (CMS) kullandığı kimlik doğrulama, içerik yönetimi,
medya ve yayınlama endpoint'lerinin resmi sözleşmesidir. Backend ekibi ile web ekibi
arasındaki kalıcı anlaşma budur.

Mobil uygulamanın kullandığı üç senkronizasyon endpoint'i (`manifest`/`snapshot`/
`changes`) bu dokümanın kapsamı DIŞINDADIR — onlar için ayrı, anonim bir sözleşme var:
**[`Sync-Sozlesmesi.md`](Sync-Sozlesmesi.md)**.

---

## 1. Genel Bakış

CMS paneli, kitabın **taslak** ağacını (Book → Module → Content → ContentBlock →
Media) düzenler. Taslakta yapılan hiçbir değişiklik mobil cihazlara ulaşmaz — sadece
bir Admin **"Yayınla"** dediğinde, o anki taslağın donmuş bir kopyası mobile açılır
(bkz. §7 Yayınlama). Bu ikisi kasıtlı olarak ayrıdır: içerik düzenleme yetkisi
(Editor + Admin) ile "sahaya ne gider" yetkisi (sadece Admin) birbirinden bağımsızdır.

## 2. Temel Bilgiler

- **Base URL deseni:** `/api/v{version}` (şu an `v1`).
- **Kimlik doğrulama:** JWT Bearer token, `Authorization: Bearer {accessToken}` başlığıyla.
  `SyncController` dışındaki **her** endpoint varsayılan olarak korumalıdır (deny-by-default
  fallback policy) — `[AllowAnonymous]` sadece login/refresh/revoke aksiyonlarında var.
- Tüm başarılı yanıtlar `Content-Type: application/json`, alan adları **camelCase**.
- Hatalar RFC 9457 `ProblemDetails` şeklinde döner (bkz. §8).
- `PublishingController` ve `UsersController`'ın bazı aksiyonları ayrıca **Admin rolü**
  gerektirir (`[Authorize(Roles = "Admin")]`) — Editor bu uçlara `403 Forbidden` alır.

## 3. Kimlik Doğrulama

### 3.1 `POST /api/v1/auth/login` — `[AllowAnonymous]`

**İstek:**

```json
{ "userName": "admin", "password": "Admin!Dev123" }
```

**Başarılı yanıt (200):**

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "8f14e45fceea167a5a36...",
  "expiresAtUtc": "2026-09-03T12:00:00Z",
  "userName": "admin",
  "fullName": "Sistem Yöneticisi",
  "roles": ["Admin"]
}
```

**Hatalı kimlik bilgisi (401):** kullanıcı bulunamadı, şifre yanlış ve hesap kilitli
durumlarının **hepsi** için, kasıtlı olarak, **aynı** genel mesaj döner (kullanıcı adı
numaralandırma saldırısını önlemek için — `AuthService.LoginAsync`, iki durumu tek bir
kısa-devreli koşulla birleştirir):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Auth.InvalidCredentials",
  "status": 401,
  "detail": "Kullanıcı adı veya şifre hatalı.",
  "traceId": "00-..."
}
```

5 başarısız denemeden sonra hesap 15 dakika kilitlenir — kilitli hesap da **aynı**
`Auth.InvalidCredentials` mesajını alır, ayrı bir "hesap kilitli" mesajı **kasıtlı
olarak yoktur** (aynı numaralandırma-önleme gerekçesiyle).

Login endpoint'i ayrıca IP başına dakikada 5 istekle sınırlıdır (429 — bkz. §8).

### 3.2 `POST /api/v1/auth/refresh` — `[AllowAnonymous]`

**İstek:**

```json
{ "refreshToken": "8f14e45fceea167a5a36..." }
```

Başarılı yanıt, login ile aynı şekildedir — yeni bir access+refresh token çifti döner.
**Rotasyon:** sunulan refresh token bu istekte tek kullanımlıktır; hemen iptal edilir
ve yerine yenisi verilir (tek transaction). Zaten iptal edilmiş bir token tekrar
sunulursa (çalıntı/tekrar-kullanım sinyali), o kullanıcının **tüm** aktif refresh
token'ları iptal edilir — sadece sunulan token değil.

### 3.3 `POST /api/v1/auth/revoke` — `[AllowAnonymous]`

**İstek:** `{ "refreshToken": "..." }`. Anonim olması kasıtlıdır — çıkış işlemi refresh
token'ın kendisini kimlik bilgisi olarak kullanır; access token süresi dolmuşken de
(en yaygın çıkış senaryosu) çalışması gerekir, o yüzden geçerli bir access token
gerektirmez. **204 No Content** döner.

### 3.4 Kullanıcı Yönetimi (`UsersController`, Admin-only)

`POST /api/v1/users` — yeni bir Editor/Admin hesabı açar. **Bu, panele bir hesabın
girebileceği tek yoldur** — self-servis kayıt yoktur.

`DELETE /api/v1/users/{id}` — **kalıcı (hard) silme**, pasifleştirme değil. Silinen
kullanıcının kendi verisi (oturum, refresh token'ları) da temizlenir. **Bir Admin
hesabını silmeye çalışmak `409 Conflict` döner** — sistemde her zaman en az bir Admin
kalmasını garanti eden bilinçli bir kısıt; sadece Editor hesapları silinebilir.

## 4. İçerik Yönetimi (Book / Module / Content / ContentBlock)

Dört varlık da aynı deseni izler: Repository/UoW → `Result<T>` → DTO (Mapster) →
FluentValidation → thin Controller. Rotalar iç içedir:

```
/api/v1/books
/api/v1/books/{bookId}/modules
/api/v1/modules/{moduleId}/contents
/api/v1/contents/{contentId}/blocks
```

Standart CRUD (`GET` liste — sayfalanmış, `GET` tekil, `POST`, `PUT`, `DELETE`) her
seviyede mevcuttur; Module ve Content ayrıca `PUT .../reorder` taşır.

**`DisplayOrder` istemciden asla gelmez.** Yeni bir kayıt oluşturulduğunda servis
`max(kardeşlerin DisplayOrder) + 1` hesaplar — istemci `(ParentId, DisplayOrder)`
üzerindeki unique+partial index'e asla çarpmaz.

**Sıralama (`reorder`):** kardeşler önce geçici **negatif** `DisplayOrder` değerlerine,
sonra nihai değerlerine taşınır (tek transaction) — tek geçişli bir swap, unique index'e
geçici olarak çarpardı.

**Bir kayıt silindiğinde kalan kardeşler otomatik olarak yeniden numaralandırılır**
(2026-09-03 eklendi) — silme sonrası `DisplayOrder` dizisinde boşluk kalmaz (`0,1,3,4`
gibi bir durum artık oluşmaz, her zaman `0,1,2,3`). Bu, mobil tarafta sıralamanın
publish sonrası doğru görünmesi için gereklidir.

**Silme cascade DEĞİLDİR.** Bir Module silindiğinde altındaki Content'ler, bir Content
silindiğinde altındaki ContentBlock'lar **otomatik silinmez** — sadece kendileri
soft-delete edilir (`IsDeleted=true`), çocukları veritabanında durmaya devam eder ama
artık ulaşılamaz durumdadır (ebeveynleri gizli). Bu, `ContentBlock`'ların soft-silinmiş
bir `Module`'e ait bir `MediaId` taşımaya devam edebileceği anlamına gelir — §5'teki
medya silme koruması bu yüzden var.

`Business/Common/PagedResult.cs` tüm liste endpoint'lerinin ortak sayfalama zarfıdır.

## 5. Medya (`MediaController`)

`POST /api/v1/media` — `multipart/form-data`. Dosya tipi **sadece magic byte'lardan**
belirlenir (`ImageSignatureDetector`), istemcinin `Content-Type`'ından veya dosya
uzantısından **asla** değil.

- Desteklenen formatlar: **PNG, JPEG, GIF, WEBP**. Video/Animasyon henüz desteklenmiyor
  — yüklemeye çalışmak `400 Media.UnsupportedFormat` döner.
- Yüklenen her görsel, mobil optimizasyonu için otomatik **WebP**'ye çevrilir —
  **GIF hariç**: animasyonlu bir GIF olduğu gibi (orijinal baytlarıyla) saklanır, çünkü
  decode katmanı animasyonlu bir GIF'in sadece ilk karesini okuyabiliyor; WebP'ye
  çevirmek animasyonu kaybettirirdi.
- Checksum (SHA-256, unique-indexed) dedup sağlar: aynı bayt dizisini iki kez
  yüklerseniz, ikinci yükleme **mevcut** `Media` satırını döner, yeni dosya yazılmaz
  (eşzamanlı yükleme yarışı dahil güvenli).

`DELETE /api/v1/media/{id}` — bir `ContentBlock` hâlâ bu medyaya referans veriyorsa
`409 Conflict` döner (önce o bloğu güncelleyin/silin). Bu koruma, §4'teki
cascade-olmayan silme davranışıyla birlikte okunmalı: soft-silinmiş bir `Module`'ün
altındaki bir `ContentBlock` hâlâ bir `Media`'ya referans verebilir ve bu kontrol onu
yakalar.

`POST /api/v1/media/cleanup-orphans` (Admin-only) — hiçbir `ContentBlock` tarafından
referans verilmeyen medya dosyalarını temizler.

## 6. Yayınlama (`PublishingController`, Admin-only)

Tüm bu bölüm sadece **Admin** rolü içindir — Editor'lar `403 Forbidden` alır.

### 6.1 `GET /api/v1/books/{bookId}/publish/preview`

Hiçbir şeyi kalıcı değiştirmeden, şu an yayınlanırsa **eklenecek/değişecek/kaldırılacak**
içeriğin bir özetini döner. "Yayınla" butonundan önce gösterilir — yanlışlıkla boş/eksik
bir yayın yapılmasını önlemek için eklendi.

### 6.2 `POST /api/v1/books/{bookId}/publish`

O anki taslağın tam bir anlık görüntüsünü alır, tek bir transaction'da:
1. Yeni bir `BookPublication` satırı (`Version = max(Version) + 1`).
2. Son yayından beri **gerçekten değişen** her content için bir `PublishedContent`
   satırı (kanonik `PayloadJson` + `Checksum = SHA256(PayloadJson)`).
3. Son yayından beri kaldırılan content'ler için tombstone satırları (`IsDeleted=true`,
   `PayloadJson="{}"`) — **tam olarak bir kez** yazılır.

**Hiçbir şey değişmediyse yeni bir sürüm oluşturulmaz** — "Yayınla" bir no-op'tur,
mevcut sürüm aynen kalır.

Eşzamanlı iki publish isteği yarışırsa, `(BookId, Version)` üzerindeki unique index bunu
veritabanı seviyesinde engeller; ikinci istek `409 Conflict` alır.

### 6.3 `POST /api/v1/books/{bookId}/rollback`

**İstek:** `{ "toVersion": 15 }`. Geçmişi silmez — eski bir `SnapshotJson`'ı **yeni bir
versiyon numarasıyla** yeniden yayınlar (git `revert`'e benzer). `toVersion`, güncel
son sürümden küçük olmalıdır (`400 Validation`, aksi halde); hiç var olmamış bir sürüm
`404 NotFound` döner. **CMS taslak ağacına dokunmaz** — sadece mobilin senkronize
ettiği yayın verisini değiştirir; rollback sonrası normal "Yayınla" yapılırsa, taslak
(rollback'ten etkilenmemiş) yeniden yayınlanır ve rollback'in etkisi geri alınmış olur
— bu bilinçli bir tasarımdır.

### 6.4 `GET /api/v1/books/{bookId}/publish/history`

Kitabın tüm `BookPublication` kayıtlarını (sürüm, tarih, içerik sayısı) sayfalanmış
olarak döner — rollback için hangi sürüme dönüleceğine karar vermek amacıyla.

## 7. Sayfalama Zarfı

```json
{
  "items": [ /* ... */ ],
  "totalCount": 97,
  "page": 1,
  "pageSize": 50
}
```

## 8. Hata Sözleşmesi

RFC 9457 `ProblemDetails`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Book.NotFound",
  "status": 404,
  "detail": "Id=999 olan kitap bulunamadı.",
  "traceId": "00-..."
}
```

| `ErrorType` | HTTP | Örnek `title` |
|---|---|---|
| `Validation` | 400 | `Content.TitleRequired` |
| `NotFound` | 404 | `Book.NotFound` |
| `Conflict` | 409 | `Media.InUse`, `BookPublication.ConcurrentPublish`, `Users.CannotDeleteAdmin` |
| `Unauthorized` | 401 | `Auth.InvalidCredentials` |
| `Forbidden` | 403 | (rol yetersiz — `[Authorize(Roles=...)]`) |
| `Unexpected` | 500 | `GlobalExceptionHandler` tarafından, tek merkezi noktadan üretilir |

Rate limit aşımı (login: 5/dk/IP, global: 300/dk/IP) `429 Too Many Requests` +
`ProblemDetails` gövdesiyle döner; health-check endpoint'leri (`/health`,
`/health/ready`) rate limiting'den muaftır (orkestratör health probe'unun gerçek
trafikle aynı limite takılıp yanlışlıkla container'ı yeniden başlatmaması için).

## 9. Sözleşme Evrim Kuralı

v1'den sonra bu sözleşmeye **sadece alan eklenebilir** (additive). Var olan bir alanın
adı, tipi veya anlamı değişmez; hiçbir alan kaldırılmaz. Kırıcı bir değişiklik
gerekirse yeni bir `/api/v2` sözleşmesi açılır.

## 10. Sürüm Geçmişi

| Sürüm | Not |
|---|---|
| v1.0 | İlk teslim — auth, Book/Module/Content/ContentBlock CRUD, Media, Publishing |
| v1.1 | Rollback endpoint'i eklendi (§6.3) |
| v1.2 | GIF istisnası dokümante edildi (§5); Module/Content silmenin cascade OLMADIĞI netleştirildi (§4); DisplayOrder otomatik yeniden numaralandırma davranışı eklendi (§4) |

---

*Mobil senkronizasyon sözleşmesi için → [`Sync-Sozlesmesi.md`](Sync-Sozlesmesi.md).
Mimari kararların gerekçesi için → [`Mimari.md`](Mimari.md). Veritabanı şeması için →
[`Veritabani.md`](Veritabani.md).*
