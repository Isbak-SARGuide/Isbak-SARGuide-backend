# Isbak SAR Guide — Admin/CMS API Sözleşmesi v1.0 (Web Ekibi İçin)

Bu doküman, admin web dashboard'unun (içerik yönetim paneli) kullanacağı REST
API'nin sözleşmesidir. `docs/Sync-Sozlesmesi-v1.md` mobil/okuyucu tarafı için
neyse, bu doküman da web/admin tarafı için odur — **ayrı bir API ailesi**,
karıştırılmamalı: Sync uçları anonim ve sadece **yayınlanmış** (published)
veriyi gösterir, buradaki CMS uçları JWT gerektirir ve **taslak/canlı**
(draft) veriyi yönetir.

GET uçlarının tüm örnekleri (Books, Modules, Contents, ContentBlocks,
Media) çalışan API'ye karşı **2026-08-28'de gerçekten çağrılıp** yakalandı.
Create/Update/Delete/Reorder gövdeleri sunucuda ekstra veri oluşturmamak
için canlı çağrılmadı — onlar için gövde şekli doğrudan backend'deki C#
DTO kaynak kodundan (`Isbak_SAR_Guide.Business/DTOs/`) çıkarıldı, alan
adları ve tipleri birebir doğru ama "gerçek yakalanmış yanıt" değil, bu
ayrım her bölümde belirtiliyor.

---

## 1. Genel Bakış

- **Kim kullanır:** Admin web dashboard (içerik editörleri + yöneticiler).
- **Kimlik doğrulama: ZORUNLU.** Sync'in aksine burada her uç (aksi
  belirtilmedikçe) `Authorization: Bearer <token>` ister — backend
  deny-by-default çalışıyor (`CLAUDE.md`: "her endpoint varsayılan olarak
  korumalı, sadece `[AllowAnonymous]` işaretliler serbest").
- **İki rol var:** `Admin` ve `Editor`. Editor içerik düzenler ama
  **yayınlayamaz** — `POST /books/{id}/publish` sadece `Admin` rolüne
  kapalı (class-level `[Authorize(Roles = "Admin")]`). Kullanıcı oluşturma
  da (`POST /users`) sadece Admin.
- **Veri modeli:** `Book → Module → Content → ContentBlock (→ Media)` —
  admin panelinde gördüğünüz her şey bu ağacın **taslak** hâli; mobil/web
  okuyucu bunu görmez, sadece `POST /books/{id}/publish` çağrıldığında o
  anki hâlin donmuş bir kopyası açılır (bkz. Sync-Sozlesmesi-v1.md §1).

---

## 2. Temel Bilgiler

- **Base URL deseni:** `/api/v{version}` (şu an `v1`) — gerçek host
  ortama göre ayrıca verilir.
- **Swagger/OpenAPI:**
  - Ham OpenAPI 3.0 dokümanı: `GET /openapi/v1.json`
  - İnteraktif referans arayüzü (Scalar): `GET /scalar` — Development
    ortamında açık, "Authorize" düğmesinden token girip uçları tarayıcıdan
    deneyebilirsiniz.
- **Content-Type:** İstek/yanıt gövdeleri `application/json` (Media
  upload hariç — o `multipart/form-data`, bkz. §7).
- Alan adları **camelCase**, Türkçe karakterler düz UTF-8 (Sync
  sözleşmesindeki §2 ile aynı kural).

---

## 3. Kimlik Doğrulama

### 3.1 `POST /auth/login` — `[AllowAnonymous]`

**Gerçek istek/yanıt** (2026-08-28, `admin` kullanıcısıyla):

```json
// İstek
{
  "userName": "admin",
  "password": "Admin!Dev123"
}
```

```json
// Yanıt (200) — accessToken/refreshToken güvenlik nedeniyle kısaltıldı,
// gerçek yanıtta tam JWT string'i ve tam refresh token gelir
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...(kısaltıldı)",
  "expiresAtUtc": "2026-08-28T09:35:57.447665Z",
  "userName": "admin",
  "fullName": "Sistem Yoneticisi",
  "roles": ["Admin"],
  "refreshToken": "cmv0qN6le/LtPl+EyW6k4NiLJiK...(kısaltıldı)"
}
```

**Gerçek örnek — yanlış şifre/kullanıcı adı** (2026-08-28):

```json
// Yanıt (401) — DOLU bir ProblemDetails, §3.4'teki boş-gövde 401'den FARKLI
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.2",
  "title": "Auth.InvalidCredentials",
  "status": 401,
  "detail": "Kullanıcı adı veya şifre hatalı."
}
```

**Önemli:** Bu 401, §3.4'te tarif edilen "gövde BOŞ" 401 ile **karıştırılmamalı**
— o, JWT middleware'inin token yok/geçersiz durumunda verdiği challenge'dır.
Buradaki 401 ise `AuthService.LoginAsync`'in ürettiği bir **domain hatası**
(§12'deki `ProblemDetails` sözleşmesine uyar) — kullanıcı adı bilinmiyor
olsa da, şifre yanlış olsa da, hesap kilitli olsa da **hep aynı** generic
mesajı döner (enumeration/kilit-durumu sızıntısını önlemek için bilinçli).
İstemci, `/auth/login`'den dönen 401'i her zaman gövdeli bekleyip parse
etmeli; diğer tüm endpoint'lerdeki 401 ise boş gövdelidir (§3.4).

**Dikkat:** İstek alanı `userName`'dir, `email` **değil** — bu web
tarafında daha önce bir kez karıştırılmıştı. Yanıttaki token alanı
`accessToken`'dir, `token` değil.

- `expiresAtUtc`: access token bu andan sonra geçersiz (dev ortamında 60
  dakika, `Jwt:ExpiryMinutes`).
- `roles`: dizi — şu an `["Admin"]` veya `["Editor"]`, birden fazla rol
  teorik olarak mümkün.

### 3.2 `POST /auth/refresh` — `[AllowAnonymous]`

Access token süresi dolunca, kullanıcıyı tekrar login'e zorlamadan
yenilemek için:

```json
// İstek
{ "refreshToken": "<login yanıtından gelen refreshToken>" }
```

Yanıt şekli `login` ile birebir aynı (`LoginResponseDto`) — yeni bir
`accessToken` + yeni bir `refreshToken` (rotasyon: eski refresh token bu
çağrıyla geçersiz olur, yeni yanıttaki kullanılmalı).

### 3.3 `POST /auth/revoke` — `[AllowAnonymous]`

```json
// İstek
{ "refreshToken": "<geçersiz kılınacak refresh token>" }
```

Logout'ta çağrılır — `[AllowAnonymous]` olması bilinçli: süresi dolmuş bir
access token'la bile logout yapılabilmeli.

### 3.4 Sonraki her istekte

```
Authorization: Bearer <accessToken>
```

**Gerçek örnek — token olmadan istek** (`GET /books`, token verilmeden):

```
HTTP 401, gövde BOŞ (JSON değil, ProblemDetails değil)
```

Bu, §12'deki `ProblemDetails` hata sözleşmesinden **farklıdır** — 401,
JWT middleware'inin kimlik doğrulama zorlaması (challenge), bir domain
hatası değil. Web tarafı 401'i "token yok/geçersiz/süresi dolmuş, login'e
dön" olarak ele almalı, gövdeyi JSON diye parse etmeye çalışmamalı.

**İstisna — bu kural `POST /auth/login`'i kapsamaz:** login'in kendi 401'i
(yanlış kullanıcı adı/şifre) **dolu bir ProblemDetails** döner, bkz. §3.1.
Ayrım: `/auth/login` hariç her uçtaki 401 boş gövdelidir.

### 3.5 Kullanıcı yönetimi (`/users`)

**Auth modeli (Faz 13.6):** `UsersController` sınıf seviyesinde sadece
`[Authorize]` taşır (kimlik doğrulanmış olmak yeterli) — her uç kendi rol
gereksinimini **ayrıca** bildirir. `POST`/`GET`/`PUT .../role`/
`POST .../deactivate` eylem-seviyesinde `[Authorize(Roles = "Admin")]`
taşıdığı için Admin-only kalır; `PUT /users/me/password` ek rol kısıtı
taşımadığı için herhangi bir authenticated kullanıcı (Admin veya Editor)
kendi şifresini değiştirebilir. **Not:** ilk tasarım sınıf seviyesine
`[Authorize(Roles = "Admin")]` koyup `me/password`'e sadece `[Authorize]`
eklemekti ("eylem seviyesi sınıf seviyesini geçersiz kılar" varsayımıyla)
— canlı bir HTTP testi bunun 403 ile başarısız olduğunu gösterdi: ASP.NET
Core çoklu `[Authorize]` filtrelerini **birleştirir** (AND), en yakını
kazanmaz. Yukarıdaki (sınıf seviyesi minimal, her eylem kendi rolünü
bildirir) tasarım bunun yerine kullanılıyor.

#### `POST /users` — `Admin`

Yeni bir Editor/Admin hesabı açmanın **tek yolu** — kayıt (self sign-up)
yok. Gövde şekli (`CreateUserDto`, kaynak koddan):

```json
{
  "userName": "editor1",
  "password": "GucluBirSifre!1",
  "fullName": "Ayşe Yılmaz",
  "role": "Editor"
}
```

#### `GET /users?page=&pageSize=` — `Admin`

Sayfalı liste (`page`/`pageSize` verilmezse ya da ≤0 ise `page=1`,
`pageSize=50` — diğer liste uçlarıyla aynı kural, bkz. §10). `UserDto`
döner, `roles` ve `isLockedOut` her kullanıcı için ayrıca hesaplanır:

```json
{
  "items": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "userName": "editor1",
      "fullName": "Ayşe Yılmaz",
      "roles": ["Editor"],
      "isLockedOut": false
    }
  ],
  "totalCount": 1,
  "page": 1,
  "pageSize": 50
}
```

> `isLockedOut` sadece bu listede doldurulur — `POST /users` ve
> `PUT .../role` yanıtlarında her zaman `false` gelir (o yollar tek bir
> kullanıcının kilit durumunu ayrıca sorgulamaz).

#### `PUT /users/{id}/role` — `Admin`

Gövde: `{ "role": "Admin" }` (`"Admin"` veya `"Editor"` olmalı). Kullanıcının
**mevcut tüm rolleri** kaldırılıp yeni rol eklenir (rol değişimi, ekleme
değil) — önce yeni rol eklenir, sonra eskiler kaldırılır (tersi değil),
böylece ara bir adım başarısız olursa kullanıcı rolsüz değil fazla-rollü
kalır. Sistemdeki **son Admin**'i Editor'a düşüremezsiniz
(`400 Validation`, kod: `User.LastAdminProtected`) — kurtarılamaz bir
admin-panel kilitlenmesini önler. Başarılı yanıt güncel `UserDto`.

#### `POST /users/{id}/deactivate` — `Admin`

Gövde yok. Hard delete **değil** — Identity'nin `LockoutEnd` mekanizmasıyla
süresiz kilitler (`SetLockoutEndDateAsync(..., DateTimeOffset.MaxValue)`),
`RefreshToken.UserId`/`BookPublication.PublishedById` gibi FK'ler bozulmaz.
**Ayrıca kullanıcının tüm aktif refresh token'larını da iptal eder** —
sadece `LockoutEnd` yeterli değildir, aksi halde zaten alınmış bir refresh
token pasifleştirmeden sonra bile rotasyonla yenilenmeye devam edebilirdi.
Bir Admin **kendi hesabını** pasifleştiremez (`id` == çağıran kullanıcının
kimliği ise `400 Validation`, kod: `User.SelfDeactivationForbidden`).
Sistemdeki **son Admin** de pasifleştirilemez (`400 Validation`, kod:
`User.LastAdminProtected`). Başarılı yanıt `204 No Content`.

#### `POST /users/{id}/activate` — `Admin`

`deactivate`'in tersi — `LockoutEnd`'i kaldırır (`SetLockoutEndDateAsync(...,
null)`), kullanıcı tekrar giriş yapabilir hale gelir. Gövde yok. **İdempotent**:
zaten aktif bir kullanıcı için de `204` döner, hata değil. Refresh token'ları
geri getirmez — kullanıcı yeniden `login` olmak zorunda (beklenen davranış,
deaktivasyon sırasında iptal edilenler kalıcı olarak geçersizdir).

#### `PUT /users/me/password` — herhangi bir authenticated kullanıcı

Sadece **kendi** şifresi — `id` yok, hedef her zaman token'daki kullanıcı.
Gövde:

```json
{
  "currentPassword": "EskiSifre!1",
  "newPassword": "YeniSifre!2"
}
```

Yanlış `currentPassword` ya da Identity şifre politikasına uymayan
`newPassword` → `400 Validation`. Başarılı olursa, çalınmış olabilecek bir
kimlik bilgisi senaryosuna karşı kullanıcının **tüm aktif refresh
token'ları iptal edilir** (deactivate ile aynı gerekçe) — bu isteği yapan
istemcinin mevcut access token'ı kendi süresi dolana kadar geçerli kalır,
ama refresh token'ı da dahil her cihaz/oturum bir sonraki yenilemede
`401` alır ve yeniden login olmak zorunda kalır. Başarılı yanıt
`204 No Content`.

---

## 4. Books

### `GET /books` — liste

**Gerçek yanıt:**

```json
[
  {
    "id": 1,
    "title": "Kentsel Arama Kurtarma El Kitabı",
    "slug": "kentsel-arama-kurtarma-el-kitabi",
    "description": "Kentsel arama kurtarma operasyonlarında görev alan ekipler için temel başvuru kaynağı.",
    "languageCode": "tr",
    "version": 16,
    "isPublished": false,
    "createdAt": "2026-08-26T10:30:33.618546Z",
    "updatedAt": "2026-08-27T11:11:12.177117Z"
  }
]
```

> `version`/`isPublished` burada **kitabın kendi** alanlarıdır — `version`
> son yayının sürümü, `isPublished` en az bir kez yayınlanıp
> yayınlanmadığını gösterir (draft tarafı sürekli düzenlenebilir, bu
> bayrak onu etkilemez).

### `GET /books/{id}` — tekil

Yukarıdaki gibi tek bir obje döner. **Gerçek 404 örneği** (`id=9999`):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Book.NotFound",
  "status": 404,
  "detail": "Id=9999 olan kitap bulunamadı.",
  "traceId": "00-016ebdf9399d2b8a18a9ecf502aeb34c-8160bc6947e6ae45-00"
}
```

### `POST /books` — oluştur

Gövde (`CreateBookDto`):

```json
{
  "title": "Yeni Kitap",
  "slug": "yeni-kitap",
  "description": "opsiyonel",
  "languageCode": "tr"
}
```

**Gerçek 400 örneği** (`title` boş, `slug` geçersiz karakter içeriyor):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "Book.ValidationFailed",
  "status": 400,
  "detail": "'Title' boş olmamalı.; Slug sadece kucuk harf, rakam ve tire icerebilir (orn. 'kentsel-arama-kurtarma').",
  "traceId": "00-f444b87086d0902eb7c0b275ed4af5ff-d56f188e6124a644-00"
}
```

> `detail` birden fazla validasyon hatasını `; ` ile birleştirilmiş tek bir
> string olarak taşır — alan bazlı bir hata listesi (`errors: {...}`)
> **değildir**. Web tarafı bu string'i olduğu gibi göstermeli veya `; `'a
> göre bölüp madde madde sunmalı.

### `PUT /books/{id}` — güncelle

Gövde (`UpdateBookDto`) — `CreateBookDto` ile aynı 4 alan. 200 + güncel
`BookDto` döner.

### `DELETE /books/{id}` — sil (soft delete)

Gövde yok, başarıda **204 No Content**.

---

## 5. Modules

Route **nested**, kitabın altında: `/books/{bookId}/modules`.

### `GET /books/{bookId}/modules?page=&pageSize=&isPublished=`

**Gerçek yanıt** (`page=1&pageSize=3`, kitapta 10 modül var, 3'ü
gösteriliyor):

```json
{
  "items": [
    {
      "id": 1,
      "bookId": 1,
      "name": "BSAFE",
      "description": "Sahada kisisel guvenlik icin temel kurallar ve senaryo bazli davranis rehberi.",
      "displayOrder": 0,
      "isPublished": false,
      "createdAt": "2026-08-26T10:30:33.618546Z",
      "updatedAt": "2026-08-26T10:30:33.618546Z",
      "contentCount": 12
    },
    {
      "id": 2,
      "bookId": 1,
      "name": "Olay Yönetimi",
      "description": "Olay yerinde sinyalizasyon, güvenlik protokolleri ve operasyonel yönetim esasları.",
      "displayOrder": 1,
      "isPublished": false,
      "createdAt": "2026-08-26T10:44:40.550774Z",
      "updatedAt": "2026-08-26T10:44:40.550774Z",
      "contentCount": 9
    }
  ],
  "totalCount": 10,
  "page": 1,
  "pageSize": 3
}
```

(Üçüncü öğe okunabilirlik için kısaltıldı — şekli aynı.) `page`/`pageSize`
verilmezse (veya ≤0) sunucu `page=1`, `pageSize=50` varsayar. Sayfalama
zarfının genel şekli için §10.

**`contentCount` (Faz 13.4, additive):** modülün altındaki soft-delete
edilmemiş Content sayısı, admin panelin her modül için ayrı bir "kaç içerik
var" çağrısı yapmasını (N+1) önlemek için tek sorguda hesaplanıp döner.
**Sadece bu liste ucunda dolu** — `GET .../modules/{id}` (tekil), `POST`,
`PUT` ve `PUT .../reorder` yanıtlarında her zaman `0` gelir (bu uçlar tek bir
modülü sayan ayrı bir sorgu çalıştırmaz).

### `GET /books/{bookId}/modules/{id}` — tekil

**Gerçek yanıt** (`id=2`):

```json
{
  "id": 2,
  "bookId": 1,
  "name": "Olay Yönetimi",
  "description": "Olay yerinde sinyalizasyon, güvenlik protokolleri ve operasyonel yönetim esasları.",
  "displayOrder": 1,
  "isPublished": false,
  "createdAt": "2026-08-26T10:44:40.550774Z",
  "updatedAt": "2026-08-26T10:44:40.550774Z"
}
```

### `POST /books/{bookId}/modules` — oluştur

Gövde (`CreateModuleDto`) — **`displayOrder` yok, göndermeyin**, sunucu
otomatik atar (mevcut son sıradan +1):

```json
{
  "name": "Yeni Modül",
  "description": "opsiyonel, max 2000 karakter",
  "isPublished": false
}
```

### `PUT /books/{bookId}/modules/{id}` — güncelle

Gövde (`UpdateModuleDto`) — aynı 3 alan (`name`, `description`,
`isPublished`), yine `displayOrder` yok.

### `DELETE /books/{bookId}/modules/{id}`

204, soft delete. **Modülün altındaki tüm Content/ContentBlock'lar da
cascade soft-delete olur** (backend'de eager-load edilip tek tek
işaretlenir).

### `PUT /books/{bookId}/modules/reorder` — sırala

Gövde (`ReorderDto`) — **modülün TÜM kardeşlerinin id'lerini**, istenen
yeni sırayla, eksiksiz içermeli:

```json
{ "orderedIds": [1, 3, 2, 4, 5, 6, 7, 8, 10, 11] }
```

Eksik/fazla/tekrar eden id → 400 Validation hatası (hiçbir şey
değişmeden). Tüm kardeşleri her seferinde eksiksiz göndermeniz gerekiyor
— kısmi/tekil "şunu üste taşı" isteği desteklenmiyor.

---

## 6. Contents

Route **nested**, modülün altında: `/modules/{moduleId}/contents`.

### `GET /modules/{moduleId}/contents?page=&pageSize=&isPublished=`

**Gerçek yanıt** (`moduleId=2`, `page=1&pageSize=2`, modülde 3 content
var):

```json
{
  "items": [
    {
      "id": 2,
      "moduleId": 2,
      "title": "Sinyaller ve Uyarılar",
      "summary": "Düdük, korna ve telsizle verilen standart olay yeri sinyalleri ile ışık sopası renk kodları.",
      "displayOrder": 0,
      "isPublished": false,
      "variantGroupKey": null,
      "variantLabel": null,
      "createdAt": "2026-08-26T10:44:40.55451Z",
      "updatedAt": "2026-08-26T10:44:40.55451Z"
    },
    {
      "id": 3,
      "moduleId": 2,
      "title": "Olay Yeri Güvenlik Hususları",
      "summary": "Olay yerinde çalışma alanının güvenli hale getirilmesi için kontrol edilmesi gereken maddeler.",
      "displayOrder": 1,
      "isPublished": false,
      "variantGroupKey": null,
      "variantLabel": null,
      "createdAt": "2026-08-26T10:44:40.558064Z",
      "updatedAt": "2026-08-26T10:44:40.558064Z"
    }
  ],
  "totalCount": 3,
  "page": 1,
  "pageSize": 2
}
```

> Bu uç `blocks[]` **taşımaz** — sadece Content'in kendi alanları. Blokları
> görmek için §7'ye ayrı bir istek atmanız gerekir (liste ekranı hafif
> kalsın diye bilinçli ayrım).

### `GET /books/{bookId}/contents?page=&pageSize=&isPublished=`

Yukarıdakinin **kitap genelinde düz (flat)** hâli — web ekibinin geri
bildirimi (Frontend-Notlar-ve-Oneriler.md madde 5): admin panelin
"İçerikler" ekranı tüm modüllerdeki tüm content'leri tek listede
göstermek için önce `GET /books/{bookId}/modules`, sonra her modül için
ayrı `GET /modules/{moduleId}/contents` çağırıyordu (N+1). Bu uç aynı
`ContentDto` şeklini (yanıt zarfı dahil) tek çağrıda, **modül sırası →
modül içi `displayOrder`** ile döner — hangi modülden geldiği `moduleId`
alanından okunur, ayrıca bir gruplama alanı eklenmedi. Route sınıf
şablonunun (`/modules/{moduleId}/contents`) altında değil, aynı kaynağın
(Content) kitap-scope kardeşi — mutlak route override (`PublishingController`
ile aynı desen). `bookId` yoksa `404`, kod: `Book.NotFound`.

### `GET /modules/{moduleId}/contents/{id}` — tekil

Yukarıdaki şeklin tekil hâli, yine `blocks[]` içermez.

### `POST /modules/{moduleId}/contents` — oluştur

Gövde (`CreateContentDto`) — `displayOrder` yine otomatik:

```json
{
  "title": "Yeni Konu",
  "summary": "opsiyonel, max 500 karakter",
  "isPublished": false,
  "variantGroupKey": null,
  "variantLabel": null
}
```

`variantGroupKey`/`variantLabel` sadece bu konu, aynı başlık altında
birden fazla varyantı olan bir grubun parçasıysa doldurulur (örn. düğüm
türleri F8/F9/TH/ABK) — bkz. `Sync-Sozlesmesi-v1.md` §3.2. Tekil
konularda ikisi de `null` bırakılmalı.

### `PUT /modules/{moduleId}/contents/{id}` — güncelle

Gövde (`UpdateContentDto`) — aynı alanlar.

### `DELETE /modules/{moduleId}/contents/{id}`

204, soft delete, altındaki bloklar cascade silinir.

### `PUT /modules/{moduleId}/contents/reorder`

Modules'daki ile aynı desen (§5) — `{ "orderedIds": [...] }`, o modülün
tüm content kardeşlerini eksiksiz içermeli.

---

## 7. ContentBlocks

Route **nested**, content'in altında: `/contents/{contentId}/blocks`.
`ContentBlockType`: Text=1, Image=2, Video=3, Animation=4, Warning=5,
Table=6.

### `GET /contents/{contentId}/blocks?page=&pageSize=`

**Gerçek yanıt** (`contentId=2`, `page=1&pageSize=2`, content'te 4 blok
var):

```json
{
  "items": [
    {
      "id": 15,
      "contentId": 2,
      "type": 6,
      "text": null,
      "dataJson": "{\"rows\": [[\"İşi Durdurma / Herkesi Susturma\", \"1 Uzun Sinyal (3 saniye)\"], [\"Acil Alan Tahliyesi\", \"3 Kısa Sinyal (1 saniye)\"], [\"İşine Devam Edebilirsin\", \"1 Uzun 1 Kısa Sinyal\"]], \"headers\": [\"İşaret\", \"Sinyal\"]}",
      "mediaId": null,
      "displayOrder": 0,
      "createdAt": "2026-08-26T10:44:40.555924Z",
      "updatedAt": "2026-08-26T10:44:40.555924Z"
    },
    {
      "id": 16,
      "contentId": 2,
      "type": 1,
      "text": "Düdük, havalı korna veya telsiz ile verilir.\n- Tahliye sonrasında, alanı tahliye eden tüm personel için telsiz anonsu geçin.",
      "dataJson": null,
      "mediaId": null,
      "displayOrder": 1,
      "createdAt": "2026-08-26T10:44:40.555924Z",
      "updatedAt": "2026-08-26T10:44:40.555924Z"
    }
  ],
  "totalCount": 4,
  "page": 1,
  "pageSize": 2
}
```

> **Dikkat — bu şekil Sync sözleşmesindekiyle aynı değil:** burada
> `mediaId` (int, bir `Media` satırına referans) var; Sync tarafında
> (`Sync-Sozlesmesi-v1.md` §3.2) ise gömülü bir `media: {id, url,
> checksum, size}` objesi vardı. CMS tarafı ham referansı taşır, Sync
> tarafı publish anında bunu zenginleştirilmiş bir özete çevirir. Web
> panelinde bir görseli göstermek için önce `mediaId` ile `GET
> /media/{id}`'den `storagePath`'i almanız gerekir (§8).

### `GET /contents/{contentId}/blocks/{id}` — tekil

Yukarıdaki şeklin tekil hâli.

### `POST /contents/{contentId}/blocks` — oluştur

Gövde (`CreateContentBlockDto`):

```json
{
  "type": 1,
  "text": "Blok metni (Text/Warning için)",
  "dataJson": null,
  "mediaId": null
}
```

- Text (1): `text` dolu, diğerleri boş.
- Image/Video (2/3): `mediaId` dolu (önce §8'den medyayı yükleyip id'sini
  alın), `text`/`dataJson` genelde boş.
- Warning (5): `text` dolu; bu kitapta `dataJson` kullanılmıyor ama alan
  destekleniyor (örn. `{"severity":"high"}`).
- Table (6): `dataJson` dolu, string olarak gömülü JSON —
  `{"headers":[...], "rows":[[...]]}` — çift-encode edilmiş olmasına
  dikkat edin (`JSON.stringify` iki kez değil, `dataJson`'ın kendisi bir
  string alan, içeriği ayrıca stringify edilmiş JSON).

### `PUT /contents/{contentId}/blocks/{id}` — güncelle

Aynı gövde şekli.

### `DELETE /contents/{contentId}/blocks/{id}`

204, soft delete.

### `PUT /contents/{contentId}/blocks/reorder`

Aynı desen — `{ "orderedIds": [...] }`.

---

## 8. Media

Route: `/media` (kitap/modül/content'e nested **değil** — medya
bağımsız bir kaynak, bloklar ona `mediaId` ile referans verir).

### `POST /media` — yükle

`multipart/form-data`, form alanı adı **`file`**:

```
POST /api/v1/media
Content-Type: multipart/form-data; boundary=...

------BOUNDARY
Content-Disposition: form-data; name="file"; filename="ornek.png"
Content-Type: image/png

<binary>
------BOUNDARY--
```

201 + oluşturulan `MediaDto` döner (şekli aşağıdaki GET örneğiyle aynı).
Max dosya boyutu `Storage:MaxFileSizeBytes` (dev'de ~20MB); magic-byte +
MIME doğrulaması yapılıyor, sadece uzantıya güvenilmiyor.

**Faz 12.7 (mobil optimizasyon) — sadece bu özellikten SONRAKİ yüklemeler:**
storage'a yazılan asıl dosya artık her zaman **WebP**'ye çevrilmiş hali
(`contentType` her zaman `"image/webp"`, `storagePath` her zaman `.webp` ile
biter — yüklenen orijinal format PNG/JPEG/GIF/WEBP fark etmez). Ayrıca küçük
bir WebP önizleme (`thumbnailStoragePath`, en uzun kenarı `Storage:
ThumbnailMaxDimension` — varsayılan 400px — ile sınırlı) üretilir. Bu
özellikten ÖNCE yüklenmiş medya geriye dönük dönüştürülmedi —
`thumbnailStoragePath` o satırlarda `null` kalır, istemci bunu "önizleme
yok, `storagePath`'i kullan" olarak ele almalı.

### `GET /media/{id}` — tekil

**Gerçek yanıt** (`id=1` — Faz 12.7 ÖNCESİ içe aktarılmış 93 medyadan biri,
bu yüzden `thumbnailStoragePath` bilerek `null`; yeni bir yükleme dolu gelir):

```json
{
  "id": 1,
  "fileName": "sektor-ceyrek-diyagrami.png",
  "storagePath": "media/kentsel-arama-kurtarma-el-kitabi/sektor-ceyrek-diyagrami.png",
  "mediaType": 1,
  "contentType": "image/png",
  "fileSize": 37898,
  "checksum": "AC5F5E0B6C81D6CEE012B91C7E9F03519D19337C80F0CB76F9B0D6DD24F1E4EB",
  "width": 551,
  "height": 372,
  "duration": null,
  "createdAt": "2026-08-26T10:53:46.490204Z",
  "thumbnailStoragePath": null
}
```

**Dikkat:** Alan `storagePath`'tir, `url` **değil** — bu, web tarafında
daha önce bir kez `data.url` diye okunup `undefined` hatasına yol açmıştı.
Görseli göstermek için `` `${API_BASE_URL}/${storagePath}` `` (ya da
storage sunucunuzun kökü neyse onunla) birleştirin. `mediaType`: Image=1,
Video=2, Animation=3, Document=4.

### `DELETE /media/{id}`

204, soft delete. Hangi bloğun bu medyayı kullandığını **önce siz**
kontrol edin — backend, referans veren bir blok varken silmenizi
engellemez (blok `mediaId`'si `null`'a düşer, blok görselsiz kalır).

---

## 9. Publishing

### `POST /books/{bookId}/publish` — sadece `Admin`

Gövde yok. O anki taslak ağacın tamamını donmuş bir sürüm olarak açar
(tek transaction). **Gerçek yanıt** (bu kitabın bir önceki publish'inden,
şekli değişmedi):

```json
{
  "publicationId": 12,
  "bookId": 1,
  "version": 16,
  "contentCount": 97,
  "checksum": "7474124FE509AF711CA283468B2712ED22F1A53BC2972A407FC870575D1DF269",
  "publishedAt": "2026-08-26T20:44:05.4212246Z"
}
```

> `checksum` burada `ManifestJson`'ın özeti — mobil/web okuyucunun
> gördüğü `Sync-Sozlesmesi-v1.md` §3.1'deki `manifest.checksum` ile aynı
> değer. İki farklı yayında aynı `(bookId, version)` çifti için yarış
> olursa (iki admin aynı anda yayınlarsa) ikinci istek `409 Conflict`
> alır — tekrar deneyin.

### `POST /books/{bookId}/rollback` — sadece `Admin`

Geçmiş bir sürümü **yeni bir sürüm olarak** tekrar yayınlar (immutable
publication modeli gereği "geri alma" değil, `git revert` gibi — eski satırlar
değişmez, sadece yeni bir tanesi eklenir). CMS'teki taslak ağaca **hiç
dokunmaz**, sadece mobil/web'in gördüğü yayını etkiler. Route sınıf
şablonunun (`.../publish`) altında değil, mutlak override ile kardeş bir
kaynak olarak tanımlı. Gövde:

```json
{ "toVersion": 1 }
```

Başarılı yanıt, `publish` ile birebir aynı şekilde `PublishResultDto` döner
(yeni `version` = `max(mevcut) + 1`). Hatalar:

- `toVersion` mevcut en son sürüme eşit ya da ondan büyükse → `400 Validation`,
  kod: `Publishing.RollbackTargetNotOlder` (rollback her zaman **geriye**
  gitmeli).
- `bookId` ya da `toVersion` yoksa → `404`, kod: `Publishing.BookNotFound`
  / `Publishing.VersionNotFound`.

### `GET /books/{bookId}/publications` — sadece `Admin`

Kitabın **tüm yayın geçmişini** (en yeniden eskiye) döner — `rollback`'in
`toVersion` girdisini elle ezberlemek yerine gerçek bir sürüm listesi/dropdown
kurmak için (web ekibinin geri bildirimi). `SnapshotJson` (megabaytlik kolon)
**hiç dönmez**, sadece özet:

```json
[
  {
    "publicationId": 12,
    "version": 16,
    "publishedAt": "2026-08-26T20:44:05.4212246Z",
    "publishedByUserName": "admin",
    "contentCount": 97,
    "checksum": "7474124FE509AF711CA283468B2712ED22F1A53BC2972A407FC870575D1DF269"
  }
]
```

Kitap hiç yayınlanmamışsa boş dizi döner (hata değil). `bookId` yoksa `404`,
kod: `Publishing.BookNotFound`.

---

## 10. Sayfalama

Modules/Contents/ContentBlocks liste uçlarının hepsi aynı zarfı kullanır
(`PagedResult<T>`):

| Alan | Tip | Açıklama |
|---|---|---|
| `items` | array | O sayfadaki kayıtlar |
| `totalCount` | int | Filtre uygulanmış toplam kayıt sayısı (sayfa boyutundan bağımsız) |
| `page` | int | Yankı — istekte verdiğiniz (veya varsayılan `1`) |
| `pageSize` | int | Yankı — istekte verdiğiniz (veya varsayılan `50`) |

`isPublished` query parametresi (Modules/Contents'te) opsiyonel bir
filtredir — verilmezse hem yayınlanmış hem taslak kayıtlar gelir.

---

## 11. Hata Sözleşmesi

Domain hataları (404/400/409) [RFC 9110 `ProblemDetails`](https://www.rfc-editor.org/rfc/rfc9457)
şeklinde döner — Sync sözleşmesindeki §6 ile aynı format
(`type`/`title`/`status`/`detail`/`traceId`). Ayrım yine **`title`**
alanına bakarak yapılmalı, `status`'a değil — aynı 404, `Book.NotFound`
ile `Module.NotFound` gibi farklı anlamlara gelebilir.

**401 farklı bir yol izler** (§3.4) — JWT middleware'inin kimlik
doğrulama zorlaması, gövdesi **boştur**, `ProblemDetails` değildir. **403
Forbidden** ise (rol yetmiyor — örn. Editor'ün publish denemesi) de
kimlik doğrulanmış ama yetkisiz durumunda döner; bu da genelde boş
gövdeli standart bir framework yanıtıdır, domain `ProblemDetails`'i
değil.

| HTTP | Ne zaman |
|---|---|
| 401 | Token yok / geçersiz / süresi dolmuş |
| 403 | Token geçerli ama rol yetersiz (örn. Editor → publish) |
| 404 | Kaynak yok (`ProblemDetails`, `title` ile ayırt edin) |
| 400 | Validasyon hatası (`ProblemDetails`, `detail` birden fazla hatayı `; ` ile birleştirir) |
| 409 | Eşzamanlı yayın çakışması (sadece publish) |

---

## 12. Sözleşme Evrim Kuralı

Sync sözleşmesindeki §9 ile aynı ilke: v1.0'dan sonra **sadece alan
eklenebilir** (additive), var olan bir alan kaldırılmaz/anlamı
değişmez. Kırıcı bir değişiklik gerekirse `/api/v2` altında yeni bir
sözleşme açılır.

---

## 13. Sürüm Geçmişi

| Sürüm | Tarih | Not |
|---|---|---|
| v1.0 | 2026-08-28 | İlk teslim — Auth, Books, Modules, Contents, ContentBlocks, Media, Publishing; GET uçlarının tamamı gerçek API yanıtlarıyla doğrulandı |
