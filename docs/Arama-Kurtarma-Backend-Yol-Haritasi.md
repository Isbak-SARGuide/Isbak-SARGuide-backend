# Isbak_SAR_Guide Backend — Yol Haritası

Kentsel arama-kurtarma el kitabı için ASP.NET Core REST API'sinin mimari kararları, iş kırılımı ve uygulama sırası.

**Durum:** Faz 0-8 tamamlandı (M0-M4 kritik yol + M5 CMS + M6 Media + M7 Auth + M8 Release
Readiness — Dockerfile/compose.prod.yaml, health check'ler, güvenlik başlıkları, rate limiting
hepsi kodda doğrulandı) · Sırada Faz 9/M9 Hardening (ETag, cache, rollback, coverage denetimi —
**post-MVP**, MVP'nin kendisi tamam)
**Son güncelleme:** 28 Ağustos 2026

---

## 1. Karar Künyesi

| Konu | Karar | Gerekçe |
|---|---|---|
| Mimari | N-Tier korunuyor (API / Business / DataAccess / Entities) | Mevcut iskelet doğru kurulmuş, tek kişilik ekipte yeterli |
| Veritabanı | PostgreSQL 18 + EF Core 10 | Zaten kurulu, jsonb desteği sync tasarımı için kritik |
| Mobil kimlik doğrulama | **Yok** — anonim okuma | Kullanıcı hesabı gereksinimi yok, kapsam küçülüyor |
| Admin kimlik doğrulama | ASP.NET Core Identity + JWT + roller | Standart, Identity paketleri zaten referanslı |
| Offline strateji | **Sürüm + delta senkronizasyonu** | Saha koşullarında şebeke yok; tam paket indirme içerik büyüdükçe verimsiz |
| Medya depolama | Local disk + `IStorageService` soyutlaması | Sıfır ek altyapı; MinIO'ya geçiş tek sınıf |
| Medya + DB ilişkisi | DB'de metadata + path + checksum, **binary DB'de değil** | Şişkin veritabanı ve yavaş yedekleme riskini önler |
| ETag / If-None-Match | İlk sürümde yok | Sözleşme oturmadan optimizasyon riskli |
| Branch stratejisi | Sadeleştirilmiş Git Flow: `main` + `develop` + `feature/*` + `fix/*` | M2'den itibaren mobilci `main`'e bağlanacak — yarım iş oraya girmemeli |
| CI / CD | CI şimdi (GitHub Actions: build + test), CD 11.3'te | Deploy edilecek imaj/hedef yok; sahte CD job'ı yanıltıcı olur |

### Branch stratejisi

| Branch | Nereden çıkar | Nereye döner | Ne zaman |
|---|---|---|---|
| `main` | — | — | **Asla doğrudan yazılmaz.** Sadece `develop`'tan PR ile. Her zaman çalışır durumda — mobilcinin ve prod'un baktığı yer |
| `develop` | `main` | `main` (faz bitince) | Günlük entegrasyon hattı |
| `feature/*` | `develop` | `develop` | Yeni iş — `feature/phase1-domain-model` |
| `fix/*` | `develop` | `develop` | Geliştirme sırasında bulunan hata |
| `hotfix/*` | **`main`** | `main` **ve** `develop` | *(production çıkınca eklenir)* |

`release/*` kullanılmıyor — sürümlü release akışı yok. `hotfix/*` production çıkana kadar ertelendi.

**Not:** `hotfix/*` kullanılmaya başlandığında `develop`'a da merge edilmesi zorunlu; unutulursa
düzeltilen hata bir sonraki sürümde geri döner.

**Branch adlandırma:** WBS görev numarası branch adına konur (`feature/1.2-build-fix`) — izlenebilirlik bedava gelir.

### Varsayımlar

1. İçerik dili sadece Türkçe. `Book.LanguageCode` yine de eklendi — ileride yeni dil = yeni `Book` satırı.
2. Video ilk sürümde yok; `MediaType` enum'u destekliyor, altyapı hazır.
3. Tek el kitabı var ama şema çoklu kitabı destekliyor.

---

## 2. Mimari

### 2.1 Katman sorumlulukları

```
Isbak_SAR_Guide.API          HTTP. İnce controller, middleware, DI wiring, auth config.
        ↓ (sadece Business'ı tanır)
Isbak_SAR_Guide.Business     İş kuralları. Service, DTO, validation, mapping, Result.
        ↓ (sadece DataAccess soyutlamalarını tanır)
Isbak_SAR_Guide.DataAccess   Kalıcılık. DbContext, EF config, Repository, UnitOfWork, migration.
        ↓
Isbak_SAR_Guide.Entities     POCO entity + enum. Hiçbir şeye bağımlı değil.
```

**Kural:** API projesi `DataAccess`'i doğrudan referans almaz. Her katman kendi DI kaydını
`AddDataAccess()` / `AddBusiness()` extension metoduyla yapar. (Dependency Inversion'ın pratik karşılığı.)

**Klasör yapısı (24 Ağustos 2026'da güncellendi — Abstract/Concrete ayrımına geçildi):**

```
Isbak_SAR_Guide.Business
├── DTOs/{Feature}              CreateBookDto, LoginDto, ...
├── Validation/{Feature}        FluentValidation validator'ları
├── Services
│   ├── Abstract                IBookService, IAuthService, ITokenService
│   └── Concrete                BookService, TokenService
├── Common                      Error, Result, JwtOptions
└── ServiceRegistration.cs

Isbak_SAR_Guide.DataAccess
├── Context / Configurations / Migrations / Seed
└── Repositories
    ├── Abstract                IRepository<T>, IUnitOfWork
    └── Concrete                EfRepository<T>, UnitOfWork
```

Gerekçe: interface ve implementasyonun nerede olduğu klasör isminden belli oluyor,
yeni katılan biri arama yapmak zorunda kalmıyor. Sınır: her private/yardımcı metot
interface'e çıkmıyor — sadece başka katmanın tükettiği gerçek soyutlamalar
(`IBookService`, `IRepository<T>` gibi) Abstract'a gider. `Entities` katmanı bu
ayrımın dışında bırakıldı (§2.3'teki mega-hiyerarşi kararıyla tutarlı).

### 2.2 Kullanılacak pattern'ler

| Pattern | Gerekçe |
|---|---|
| Repository + Unit of Work | EF'i Business'tan izole eder; publish akışı transaction gerektiriyor |
| `Result<T>` | Exception akış kontrolü için kullanılmaz; controller HTTP koduna çevirir |
| DTO (record) + Mapster | Entity dışarı sızmaz; Mapster convention-based, config minimum |
| Options Pattern | `JwtOptions`, `StorageOptions` — magic string yok |
| Strategy (hafif) | `IStorageService` → `LocalFileStorageService` / ileride `MinioStorageService` |
| Middleware | Global exception handling, request logging |
| Abstract/Concrete klasör ayrımı | Interface ve implementasyon ayrı klasörde; okunabilirlik ve keşfedilebilirlik için (Business/Services, DataAccess/Repositories) |

### 2.3 Bilinçli olarak KULLANILMAYACAKLAR

| Pattern | Neden |
|---|---|
| CQRS / MediatR | Tek kişilik ekipte aşırı boilerplate; service sınıfları yeterli |
| Specification Pattern | Bu ölçekte sorgu karmaşıklığı yok |
| Domain Events | Publish tek transaction, event'e gerek yok |
| Generic `IEntity` / `IAuditable` mega-hiyerarşisi | Sadece `BaseEntity`; fazlası soyutlama için soyutlama |
| AutoMapper Profile patlaması | Mapster daha az konfigürasyon istiyor |

---

## 3. Domain Modeli

```
Book
 └── Module
      └── Content
           └── ContentBlock ──► Media (opsiyonel)

BookPublication      (immutable yayın kaydı)
PublishedContent     (immutable içerik anlık görüntüsü)
```

### Mevcut modele eklenecekler

| Alan | Entity | Neden |
|---|---|---|
| `DataJson` (jsonb) | `ContentBlock` | Table / Warning / Animation blokları için yapısal veri; tabloyu düz metne sıkıştırmak ileride acı verir |
| `Checksum` | `Media` | Offline istemcinin indirdiği dosyayı doğrulaması için |
| `LanguageCode` | `Book` | Çok dillilik sigortası, şimdi ucuz |
| `Slug` | `Book` | Okunabilir URL |
| `IsDeleted` + `DeletedAt` | Tümü | Silinen içeriğin mobile "tombstone" olarak bildirilmesi için **zorunlu** |
| Audit alanları | `BaseEntity` | `CreatedAt` / `UpdatedAt` |

---

## 4. Offline Senkronizasyon Tasarımı

**Problem:** Admin düzenlerken mobil bunu görmemeli. Yayınlanınca mobil sadece değişeni çekmeli.
Yarım kalan indirme eski sürümü bozmamalı.

**Çözüm:** Draft tabloları ile yayın anlık görüntüsü (snapshot) ayrımı.

```
ADMIN TARAFI (canlı, serbestçe düzenlenir)
Book / Module / Content / ContentBlock / Media
                    │
                    │  POST /api/v1/books/{bookId}/publish  (Admin rolü)
                    │  (tek transaction, versiyon N → N+1)
                    ▼
YAYIN TARAFI (immutable, mobil sadece burayı görür)
BookPublication (Version, ManifestJson, Checksum)
PublishedContent (Version, ContentId, PayloadJson, Checksum, IsDeleted)
                    │
                    ▼
                 MOBİL
```

**Bu ayrımın sağladıkları:**
- Yarım kalan düzenleme mobile sızamaz (yapısal olarak imkânsız)
- Yayın geçmişi tutulur → rollback mümkün
- Delta sorgusu tek satır: `WHERE version > @fromVersion`

### 4.1 Endpoint sözleşmesi (mobil geliştiriciye verilecek)

```http
GET /api/v1/sync/manifest?bookId=1
→ { "bookId":1, "version":12, "publishedAt":"...", "contentCount":84,
    "media":[{"id":15,"url":"/media/books/1/yapi.webp",
              "checksum":"sha256:...","size":248901}],
    "checksum":"sha256:..." }

GET /api/v1/sync/snapshot?bookId=1              # ilk kurulum — tam paket
→ { "version":12, "book":{...}, "modules":[...], "contents":[...] }

GET /api/v1/sync/changes?bookId=1&fromVersion=11    # delta
→ { "fromVersion":11, "toVersion":12,
    "upsertedContents":[...], "deletedContentIds":[43,44],
    "addedMedia":[...], "removedMediaIds":[...] }
```

### 4.2 Mobil taraf akışı (bilgi amaçlı — backend sorumluluğu değil)

```
manifest çek → sürüm farkı var mı? → changes çek → checksum doğrula
→ HEPSİ tamamsa local sürümü flip et (atomic switch)
```

İndirme yarıda kalırsa eski sürüm aktif kalmalı. Checksum'ları backend sağlıyor;
atomic switch mobil geliştiricinin sorumluluğu.

---

## 5. Work Breakdown Structure

**Yol tipi:** `KRİTİK` = sıfır float · `YAKIN` = <4 sa float · `REWORK` = float var ama geciktirilirse bitmiş işi bozar · `SERBEST` = >8 sa float

### 1. Project Foundation — 3,0 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 1.1 | Git init, `.gitignore`, `obj`/`bin` temizliği | 0,5 | — | KRİTİK |
| 1.2 | `ApplicationUser` doldur, `Class1.cs` sil, `Directory.Build.props`, `.editorconfig` → **build yeşil** | 1,0 | 1.1 | KRİTİK |
| 1.3 | Paket kurulumları + .NET 10 uyum doğrulaması | 0,5 | 1.2 | KRİTİK |
| 1.4 | Test projesi iskeleti (xUnit + FluentAssertions) | 0,25 | 1.2 | YAKIN |
| 1.5 | `compose up` + PG18 bağlantı doğrulaması | 0,25 | — | KRİTİK |
| 1.6 | `global.json` + GitHub Actions CI (build + test) + GitHub remote | 0,5 | 1.1 | YAKIN |

> **1.6 — CI evet, CD hayır.** CI (her push'ta derle + test et) şimdi kurulabilir ve her commit'i korur.
> CD (deploy) şu an anlamsız: deploy edilecek bir imaj veya hedef yok. CD, **11.3'te Dockerfile
> çıktığında** eklenir. Sahte bir deploy job'ı yazmak öğretici değil, yanıltıcı olur.
>
> **Öğrenme sırası bilinçli:** CI önce eklenir ve bozuk build üzerinde **kırmızı** görülür, sonra 1.2
> ile yeşile çevrilir. İlk commit'ten beri yeşil olan bir CI'ın gerçekten derleme yapıp yapmadığı
> bilinemez — TDD'deki "önce testin başarısız olduğunu gör" kuralının CI karşılığı.

> **1.3 neden 1. saatte?** .NET 10 çok yeni. Paket uyumsuzluğunu 1. saatte öğrenmek 20. saatte öğrenmekten kıyaslanamayacak kadar ucuz. Fallback `net9.0`'a düşmek bu noktada tek satır.

### 2. Domain & Persistence — 7,0 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 2.1 | `BaseEntity`, audit + soft-delete arayüzleri | 0,5 | 1.2 | KRİTİK |
| 2.2 | İçerik entity'lerini tamamla (`DataJson`, `Checksum`, `LanguageCode`, `Slug`) | 1,0 | 2.1 | KRİTİK |
| 2.3 | `BookPublication`, `PublishedContent` entity'leri | 0,5 | 2.1 | KRİTİK |
| 2.4 | `IEntityTypeConfiguration` sınıfları, index'ler, **jsonb mapping** | 1,5 | 2.2, 2.3 | KRİTİK |
| 2.5 | `DbContext`: DbSet, query filter, audit interceptor | 1,0 | 2.4 | KRİTİK |
| 2.6 | İlk migration + PG18'e uygula + doğrula | 1,0 | 2.5, 1.5 | KRİTİK |
| 2.7 | Seed: roller, admin, **gerçekçi içerik ağacı (~80 content, ~100 media)** | 1,5 | 2.6 | KRİTİK |

> **2.7 neden kritik yolda?** Publish'i Admin CRUD'dan kurtaran şey bu. Seed olmadan publish'i test etmek için önce CRUD yazmak gerekirdi; seed ile 7 saatlik CRUD bloğu kritik yoldan çıkıyor.

### 3. API Foundation — 3,5 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 3.1 | DI kompozisyonu, API→DataAccess referansını kes | 0,5 | 2.6 | KRİTİK |
| 3.2 | `Result<T>` + `Error` tipi | 0,5 | 1.2 | KRİTİK |
| 3.3 | Exception middleware + ProblemDetails + Result→ActionResult çevirici | 1,0 | 3.2 | KRİTİK · REWORK |
| 3.4 | Serilog | 0,5 | 3.1 | SERBEST |
| 3.5 | Swagger + `/api/v1` versiyonlama + CORS | 1,0 | 3.1 | YAKIN |

### 4. Walking Skeleton — 7,5 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 4.1 | `IRepository<T>` + `EfRepository<T>` + `IUnitOfWork` (transaction) | 1,5 | 2.6, 3.1 | KRİTİK |
| 4.2 | Book DTO (record) + Mapster + FluentValidation | 1,0 | 3.2 | YAKIN |
| 4.3 | `BookService` (CRUD) | 1,0 | 4.1, 4.2 | YAKIN |
| 4.4 | `BooksController` + Swagger'dan uçtan uca doğrulama | 1,0 | 4.3, 3.3, 3.5 | YAKIN |
| 4.5 | Entegrasyon test harness'ı (Testcontainers + token helper) | 1,5 | 4.4, 6.1 | YAKIN · REWORK |
| 6.1 | **Auth çekirdeği**: Identity + JWT pipeline + `JwtOptions` + login + fallback policy | 3,0 | 2.6, 3.1 | YAKIN · **REWORK** |

> **6.1 neden burada?** Gerekçe takvim değil, yeniden-iş. `[Authorize]` ve test token helper'ı sonradan gelirse yazılmış **her entegrasyon testinin kurulumu** elden geçer.

### 5. Sync Contract Stub — 1,5 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 5.0 | Sync JSON şemasını dondur + seed'den statik JSON dönen stub endpoint'ler | 1,5 | 2.7, 3.3 | YAKIN |

> Yeni teknoloji yok — statik JSON dönen bir controller. **Mobil workstream'i ~20 saat erken açıyor** ve şemayı erken donduğu için sonraki fazda sürpriz azaltıyor.

### 6. Publishing Engine — 7,0 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 6.2 | Snapshot serialization tasarımı, payload şemasını 5.0 ile hizala | 1,0 | 5.0, 2.3 | KRİTİK |
| 6.3 | `IPublishingService`: ağaç topla → serialize → checksum → versiyon bump (**tek transaction**) | 2,5 | 6.2, 4.1 | KRİTİK |
| 6.4 | Tombstone / silme mantığı | 1,0 | 6.3 | KRİTİK |
| 6.5 | Publish senaryo testleri (draft sızmıyor · v1→v2 · silme · idempotency) | 2,0 | 6.4, 4.5 | KRİTİK |
| 6.6 | Publish endpoint (`[Authorize(Admin)]`) | 0,5 | 6.3, 6.1 | YAKIN |

### 7. Synchronization — 6,5 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 7.1 | `ISyncService.GetManifest` | 1,0 | 6.3 | KRİTİK |
| 7.2 | `GetSnapshot` (tam paket) | 1,0 | 6.3 | YAKIN |
| 7.3 | `GetChanges` (delta) | 1,5 | 6.4 | KRİTİK |
| 7.4 | `SyncController` — stub'ları gerçekle değiştir, anonim | 0,5 | 7.1-7.3 | KRİTİK |
| 7.5 | **Delta doğruluk test matrisi** | 2,0 | 7.4 | KRİTİK |
| 7.6 | Sync sözleşme dokümanı v1.0 (mobilciye resmî teslim) | 0,5 | 7.5 | YAKIN |

### 8. CMS Completion — 7,0 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 8.1 | Module service + controller | 1,0 | 4.4 | SERBEST |
| 8.2 | Content service + controller | 1,5 | 4.4 | SERBEST |
| 8.3 | ContentBlock service + controller (`DataJson` dahil) | 1,5 | 4.4 | SERBEST |
| 8.4 | Reorder işlemleri | 1,0 | 8.1-8.3 | SERBEST |
| 8.5 | Sayfalama + filtreleme | 0,5 | 8.1-8.3 | SERBEST |
| 8.6 | Service unit testleri | 1,5 | 8.1-8.4 | SERBEST |

> Tamamı `SERBEST` — 4.4'te desen kanıtlandığı için bu blok "keşif" değil "replikasyon".

### 9. Auth Feature Set — 4,0 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 9.1 | Refresh token (rotasyon + revoke) | 1,5 | 6.1 | SERBEST |
| 9.2 | Roller (Admin/Editor) + policy'leri controller'lara uygula | 1,0 | 8.x | SERBEST |
| 9.3 | Lockout ayarı + login rate limiting | 0,5 | 6.1 | SERBEST |
| 9.4 | Auth + yetkisiz erişim testleri | 1,0 | 9.1-9.3 | SERBEST |

### 10. Media Pipeline — 6,0 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 10.1 | `IStorageService` + `LocalFileStorageService` + `StorageOptions` | 1,0 | 3.1 | SERBEST |
| 10.2 | `MediaService`: upload, SHA-256, boyut/MIME/**magic-byte** doğrulama | 2,0 | 10.1 | SERBEST |
| 10.3 | Path traversal koruması + GUID isimlendirme | 0,5 | 10.2 | SERBEST |
| 10.4 | `MediaController` + statik dosya servisi | 1,0 | 10.3, 6.1 | SERBEST |
| 10.5 | Medya güvenlik testleri | 1,0 | 10.4 | SERBEST |
| 10.6 | Yetim dosya temizliği | 0,5 | 10.4 | SERBEST |

### 11. Release Readiness — 3,5 sa

| ID | Görev | Sa | Ön koşul | Yol |
|---|---|---|---|---|
| 11.1 | Health check (`/health`, `/health/ready`) | 0,5 | 3.1 | YAKIN |
| 11.2 | Security headers + HTTPS zorunluluğu + response compression | 0,5 | 3.1 | YAKIN |
| 11.3 | Dockerfile + prod compose + env dokümantasyonu + **CD job'ı (1.6'nın devamı)** | 1,5 | 11.1 | KRİTİK |
| 11.4 | `code-reviewer` + `security-reviewer` final geçişi | 1,0 | Tümü | KRİTİK |

### 12. Hardening & Optimization — 12,0 sa (post-MVP)

| ID | Görev | Sa |
|---|---|---|
| 12.1 | Payload boyut ölçümü (gerçekçi seed ile) | 1,0 |
| 12.2 | Manifest/snapshot cache + invalidation | 1,0 |
| 12.3 | `AsNoTracking` / projeksiyon denetimi, N+1 avı | 1,0 |
| 12.4 | ETag / If-None-Match | 1,0 |
| 12.5 | Coverage denetimi + eksik test tamamlama | 2,5 |
| 12.6 | Rollback / restore endpoint'i | 1,5 |
| 12.7 | WebP + thumbnail (**lisans doğrulaması sonrası**) | 2,0 |
| 12.8 | Global rate limiting | 0,5 |
| 12.9 | Public read endpoint'leri (**gerekliliği önce doğrula**) | 1,5 |

### 13. Mobil & Web Frontend Uyumluluk Düzeltmeleri — ~9,0 sa (post-MVP)

Mobil ekip (`docs/mobil_ekip_geri_bildirim_v1.1.md`) ve web frontend ekibi
(`docs/Web-Frontend-Geri-Bildirim-v2.md` + `docs/Frontend-Notlar-ve-Oneriler.md`)
gerçek çalışan backend'e karşı entegrasyon yaparken bulduğu, sözleşmeyi
bozmayan (additive) küçük boşluk/uyumsuzluklar. İki ekip de temel
sözleşmenin (alan adları, endpoint'ler, hata kodları, blok tipleri)
tamamen uyumlu olduğunu doğruladı — aşağıdakiler netleştirme/küçük
düzeltme, kırılma değil.

| ID | Görev | Kaynak | Sa |
|---|---|---|---|
| 13.1 | Sync/CMS sözleşme netleştirmeleri (medya base URL, varyant grup başlığı, CORS notu, login 401 gövdesi) | mobil #1,#2 / web #3,#4 | 1,0 |
| 13.2 | `/sync/changes`'e `book` alanı ekle | mobil #4 | 0,5 |
| 13.3 | Publish `IsPublished`'a göre süzsün + tek seferlik backfill + `Book.IsPublished` bugfix'i | web #2 | 1,5 |
| 13.4 | `ModuleDto.ContentCount` (admin panel N+1 düzeltmesi) | web #5 | 1,0 |
| 13.5 | Reorder'ın alakasız bloğun `dataJson`'ını bozmasını önle | web #6 | 1,0 |
| 13.6 | Tam `/users` CRUD (liste, rol değiştirme, pasifleştirme, kendi şifresini değiştirme) | web #7 | 3,0 |
| 13.7 | Video/Animation `dataJson` taslak şeması (provisional) | mobil #3 | 1,0 |

**Bilinçli olarak kapsam dışı:** Acil Durum Bandı backend desteği (web #1,
bkz. §14 Faz 10 notu — sonradan eklenebilir, şimdi öncelik değil); web
frontend'in kendi kod tabanındaki ölü kod/kitap seçici notları (web #8) —
backend'i ilgilendirmiyor.

---

## 6. Bağımlılık Grafiği

```
1.1 Git ──► 1.2 BUILD YEŞİL ──┬──► 1.3 Paket uyumu
                              ├──► 1.4 Test iskeleti
                              └──► 2.1 BaseEntity
1.5 Postgres ayakta ───────────────────────┐
                                           │
2.1 ──┬──► 2.2 İçerik entity ──┐           │
      └──► 2.3 Publication ────┴──► 2.4 EF Config ──► 2.5 DbContext
                                                            │
                                          ┌─────────────────┘
                                          ▼
                                   2.6 MIGRATION ◄── 1.5
                                          │
                          ┌───────────────┼──────────────────────┐
                          ▼               ▼                      ▼
                    2.7 SEED         3.1 DI ◄── 3.2 Result   6.1 AUTH ÇEKİRDEK
                          │               │         │              │
                          │      ┌────────┼─────────┤              │
                          │      ▼        ▼         ▼              │
                          │   3.4 Serilog 3.5 Swagger 3.3 Problem  │
                          │   (float)         │      Details       │
                          │                   │         │          │
                          ▼                   ▼         ▼          ▼
                   ╔═══════════════════════════════════════════════════╗
                   ║  4.1 Repo+UoW ─► 4.3 BookService ─► 4.4 Controller ║
                   ║  4.2 DTO/Mapster ──┘                     │        ║
                   ║                              4.5 Test harness ◄───╢
                   ╚═══════════════════════════════════════════════════╝
                          │                              │
              ┌───────────┴──────────┐                   │
              ▼                      ▼                   │
      5.0 SYNC STUB          [horizontal replikasyon]    │
              │                      │                   │
        ┌─────┘              8.1 Module ─┐               │
        │                    8.2 Content ├─► 8.4 Reorder │
        │                    8.3 Block  ─┘   8.5 Sayfala │
        ▼                                          │     │
   6.2 Snapshot tasarım                            ▼     │
        │                                    8.6 Unit test│
        ▼                                                 │
   6.3 PublishingService ◄── 4.1 (transaction)            │
        │                                                 │
        ├──► 6.6 Publish endpoint ◄── 6.1                 │
        ▼                                                 │
   6.4 Tombstone                                          │
        │                                                 │
        ├──────► 6.5 Publish testleri ◄────────────────────┘
        │
        ├──► 7.1 Manifest ─┐
        ├──► 7.2 Snapshot ─┼──► 7.4 SyncController ──► 7.5 Delta test
        └──► 7.3 Changes ──┘                                │
                                                            ▼
                                                    7.6 SÖZLEŞME v1.0
                                                            │
                                                            ▼
                                                    [MOBİL ENTEGRASYON]

BAĞIMSIZ DALLAR (2.6 / 3.1 / 6.1 sonrası herhangi bir zamanda):
  3.1 ──► 10.1 Storage ──► 10.2 MediaService ──► 10.3 Traversal ──► 10.4 Controller ──► 10.5 Test
                                                                        ▲
                                                                   6.1 ─┘
  6.1 ──┬─► 9.1 Refresh token ───┐
        ├─► 9.3 Lockout/RateLimit ├──► 9.4 Auth test
  8.x ──┴─► 9.2 Roller ──────────┘

  3.1 ──► 11.1 Health ──► 11.3 Dockerfile ──► 11.4 Final review
  3.1 ──► 11.2 Headers / Compression
```

**Grafikten okunan üç gerçek:**
1. `2.6 MIGRATION` tek gerçek darboğaz — 5 dal buradan çıkıyor.
2. `2.7 SEED` → `6.x PUBLISH` kenarı, 7 saatlik CRUD bloğunu kritik yoldan çıkarıyor.
3. Media (10.x) ve Auth-feature (9.x) ana gövdeye hiç geri bağlanmıyor — tam bağımsız.

---

## 7. Kritik Yol — 25,5 saat

```
1.1  Git init                     0,5
 ↓
1.2  Build yeşil                  1,0
 ↓
1.3  Paket uyum doğrulaması       0,5
 ↓
2.1  BaseEntity                   0,5
 ↓
2.2  İçerik entity tamamlama      1,0
 ↓
2.3  Publication entity           0,5
 ↓
2.4  EF Configuration (jsonb)     1,5
 ↓
2.5  DbContext                    1,0
 ↓
2.6  Migration                    1,0   ◄── DARBOĞAZ
 ↓
2.7  Seed (gerçekçi hacim)        1,5
 ↓
3.1  DI kompozisyonu              0,5
 ↓
3.3  ProblemDetails + Result      1,0
 ↓
4.1  Repository + UnitOfWork      1,5
 ↓
6.2  Snapshot tasarımı            1,0
 ↓
6.3  PublishingService            2,5   ◄── EN YÜKSEK RİSK
 ↓
6.4  Tombstone                    1,0
 ↓
6.5  Publish senaryo testleri     2,0
 ↓
7.3  GetChanges (delta)           1,5
 ↓
7.4  SyncController               0,5
 ↓
7.5  Delta test matrisi           2,0
 ↓
11.3 Dockerfile / prod config     1,5
 ↓
11.4 Final review                 1,0
```

| Sınıf | Süre | Oran | Anlamı |
|---|---|---|---|
| **Critical** (float = 0) | 25,5 sa | %44 | Kaydırılamaz, kesilemez |
| **Near Critical** (float < 4 sa) | 12,5 sa | %22 | Kaydırılabilir, kesilemez |
| **Non Critical** (float > 8 sa) | 19,5 sa | %34 | Kaydırılabilir **ve** kesilebilir |
| **MVP toplam** | **57,5 sa** | | ~7,5 iş günü |

### Tek geliştirici için kritik yolun anlamı

Tek geliştiricide her iş serileşir; toplam süre 57,5 saat kalır. Kritik yolun değeri süreyi
kısaltmak değil, üç soruyu cevaplamak:

1. **Neyi asla erteleyemem?** → 25,5 saatlik zincir. Buradaki her gecikme birebir teslime yansır.
2. **Zaman biterse neyi keserim?** → Non-critical %34. Bu havuz olmasa karar panik anında verilir.
3. **İki kişi olsak ne kazanırdım?** → Teorik minimum ~26-30 saat. İleride işe alım kararında somut girdi.

### `REWORK` sınıfı — klasik CPM'in göstermediği kategori

`6.1` Auth çekirdeği, `3.3` Result çevirici, `5.0`/`6.2` şema dondurma. Takvimde float'ları var
ama geciktirilirse **bitmiş işi bozarlar.** Bu projede en pahalı kategori bunlar.

---

## 8. Paralel Workstream'ler

```
WS-A · CORE            WS-B · CONTENT         WS-C · PUBLISH/SYNC
Foundation + Domain    Vertical slice + CMS   Publishing + Sync
1.x, 2.x, 3.x          4.x, 8.x               5.0, 6.x, 7.x
14,5 sa                14,5 sa                15,0 sa
🔴 Bloklayıcı          🟡 Ürün değeri         🔴 En yüksek risk
                                              🌍 Dış bağımlılık

WS-D · SECURITY        WS-E · MEDIA           WS-F · OPS
6.1 + 9.x              10.x                   11.x, 12.x
7,0 sa                 6,0 sa                 15,5 sa
⚠️ Rework riski        🟢 Tam bağımsız        🟢 Tam bağımsız
```

**Tek geliştirici bunları neden yine de ayrı workstream saymalı?**

1. **Bağlam değiştirme maliyeti gerçek.** Media güvenliğiyle delta sync semantiği farklı zihinsel modeller.
2. **Her workstream'in kendi "bitti" tanımı ve test stratejisi var.** WS-C senaryo testi, WS-E güvenlik testi, WS-B unit test ağırlıklı.
3. **Risk profilleri farklı → test yatırımı farklı.** WS-C'ye %90 coverage, WS-B'ye %60 mantıklı.
4. **Duraklama noktası verir.** Proje kesintiye uğrarsa workstream sınırında durursun, elinde çalışan bir şey olur.
5. **Yardım gelirse dikiş yerleri hazır.** WS-E veya WS-D sıfır çakışmayla devredilebilir.
6. **WS-C'nin dış müşterisi var.** Mobilci bekliyor — bu onu farklı bir taahhüt sınıfına sokar.

---

## 9. Faz Sırası

### Vertical Slice kararı

**Tek dilim, `Book` üzerinde, Faz 2'de.** Book seçildi çünkü en az alanlı ve en az ilişkili varlık —
altyapıyı en az gürültüyle kanıtlar. Dilimin kanıtlaması gerekenler:

```
Postgres ─ EF/jsonb ─ Repository/UoW ─ Service ─ DTO/Mapster
   ─ FluentValidation ─ Result<T> ─ ProblemDetails ─ JWT ─ Swagger ─ Testcontainers
```

Bu zincir bir kez yeşile döndüğünde Module/Content/ContentBlock **replikasyon** olur, keşif değil.
Bu yüzden Faz 5'te 3 varlık 4 saatte bitiyor — Book tek başına 4,5 saat sürerken.

**Neden vertical slice sadece bir kez?** Dikey dilimin değeri bilinmeyeni ortaya çıkarmaktır.
Book dilimi bittiğinde bilinmeyen kalmaz; Module için ikinci dilim sıfır bilgi üretir.

### Revize faz sırası

```
PHASE 0 — Foundation & Green Build              3,0 sa   → M0
          1.1 · 1.6 (CI, kırmızı) · 1.2 (CI yeşil) · 1.3 · 1.4 · 1.5

PHASE 1 — Domain, Persistence & Seed            7,0 sa   → M1
          2.1 → 2.7

PHASE 2 — Walking Skeleton + Contract Stub     12,5 sa   → M2  ⭐
          3.1-3.5 · 6.1 Auth çekirdeği · 4.1-4.5 Book dilimi · 5.0 Sync stub
          ➜ MOBİL GELİŞTİRİCİ BURADA BAŞLAR

PHASE 3 — Publishing Engine                     7,0 sa   → M3  ⭐
          6.2 → 6.6      ➜ EN BÜYÜK BELİRSİZLİK BURADA ÖLÜR

PHASE 4 — Synchronization                       6,5 sa   → M4  ⭐
          7.1 → 7.6      ➜ SÖZLEŞME DONAR, MOBİL ENTEGRASYON BAŞLAR

──────── buraya kadar: 35,5 sa · kritik yolun tamamı bitti ────────

PHASE 5 — CMS Completion      (horizontal)      7,0 sa   → M5
PHASE 6 — Media Pipeline      (paralel dal)     6,0 sa   → M6
PHASE 7 — Auth Feature Set    (paralel dal)     4,0 sa   → M7
PHASE 8 — Release Readiness                     3,5 sa   → M8
                                           ══ MVP TAMAM: 56 sa ══
PHASE 9 — Hardening & Optimization             12,0 sa   → M9
                                        ══ PRODUCTION: ~68 sa ══
PHASE 10 — Mobil & Web Uyumluluk Düzeltmeleri    9,0 sa   → M10
                                    ══ TOPLAM: ~77 sa ══
```

### Orijinal plana göre değişenler

| Değişiklik | Gerekçe |
|---|---|
| Publish/Sync: 8. sıradan **3-4. sıraya** | Risk-first. Belirsizlik 32. saat yerine 23. saatte ölüyor |
| Auth çekirdeği: 5. fazdan **2. faza** | Rework önleme — test harness'ı token'lı doğuyor |
| Auth feature seti: **7. faza** | Refresh/lockout/roller hiçbir şeyi bloklamıyor |
| CMS: 4. sıradan **5. sıraya** | Seed sayesinde publish'i bloklamıyor |
| Media: 6. sıradan **bağımsız dala** | Publish'i hiç bloklamıyordu |
| Sync stub (5.0) **yeni** | Mobilciyi 20 saat erken açıyor, şemayı erken donduruyor |
| Public read: **Faz 9'a**, gereklilik doğrulaması şartıyla | Offline-first mimaride müşterisi belirsiz |
| Eski Faz 9 ikiye bölündü | "Ucuz+zorunlu" ile "pahalı+ertelenebilir" ayrıldı |

---

## 10. MVP / Production Kapsamı

### MVP — 56 saat

**Dahil olma kriteri:** Yokluğunda ürün çalışmaz, VEYA sonradan eklenince bitmiş işi bozar, VEYA güvenlik açığı bırakır.

| Kategori | Kapsam |
|---|---|
| Zemin | Build yeşil · git · paket uyumu · test iskeleti · Postgres |
| Domain | Tüm entity'ler · EF config · jsonb · migration · gerçekçi seed |
| API zemini | DI · Result · ProblemDetails · Swagger · versiyonlama · CORS · Serilog |
| Veri erişimi | Repository · UnitOfWork (transaction) |
| Auth | Identity · JWT · login · roller · `[Authorize]` · login rate limit · refresh token (basit) |
| CMS | Book/Module/Content/ContentBlock CRUD · reorder · sayfalama |
| Media | Upload · checksum · MIME+magic-byte · path traversal koruması · servis |
| Publish | Snapshot üretimi · tombstone · transaction · versiyon bump |
| Sync | manifest · snapshot · changes · sözleşme dokümanı |
| Release | Health check · security headers · compression · Dockerfile · prod env |
| Test | Publish/Sync senaryo matrisi · media güvenlik · auth · service unit |

**MVP'ye özellikle dahil edilenler:**

| Kalem | Neden ertelenemez |
|---|---|
| Login rate limiting (0,5 sa) | Gerçek saldırı yüzeyi, maliyeti yarım saat |
| Response compression (0,25 sa) | Tek satır konfigürasyon, snapshot payload'ında en büyük tek kazanç |
| Health check (0,5 sa) | Deploy edilemeyen sistem MVP değildir |
| Magic-byte doğrulama | Uzantı bazlı doğrulama güvenlik değildir |
| Gerçekçi seed (1,5 sa) | 7 saatlik CRUD bloğunu kritik yoldan çıkarıyor — 5 katı geri ödüyor |

### Production Hardening — +12 saat

**Erteleme kriteri:** Sonradan eklendiğinde hiçbir sözleşmeyi ve hiçbir bitmiş işi bozmuyor.

| Kalem | Sa | Erteleme gerekçesi | Ne zaman |
|---|---|---|---|
| Payload boyut ölçümü | 1,0 | Optimizasyon kararlarının girdisi | M8'den hemen sonra |
| Manifest/snapshot cache | 1,0 | Ölçmeden cache = YAGNI | Ölçüm gösterirse |
| N+1 / projeksiyon denetimi | 1,0 | Gerçek trafik olmadan spekülatif | İlk yük sonrası |
| ETag / If-None-Match | 1,0 | Sözleşme oturmadan optimizasyon riskli | Mobil entegrasyon stabil olunca |
| Coverage %80 agregat denetimi | 2,5 | Kritik kod zaten sürekli test edildi | M8 sonrası |
| Rollback / restore | 1,5 | Immutable model rollback'i mümkün kılıyor; endpoint ayrı iş | İlk prod yayından önce |
| WebP + thumbnail | 2,0 | Checksum değişir → versiyonlama halleder | Medya hacmi artınca |
| Global rate limiting | 0,5 | Anonim sync endpoint'i tek risk | İlk prod yayından önce |
| Public read endpoint'leri | 1,5 | Önce müşterisi olduğunu doğrula | Somut talep gelirse |
| MinIO geçişi | 1,5 | `IStorageService` sayesinde tek sınıf | Disk/ölçek baskısı olunca |

### Test coverage duruşu

%80 agregat hedefi korunuyor ama **sona bırakılmıyor ve düz dağıtılmıyor.** Risk ağırlıklı:

| Alan | Hedef | Ne zaman |
|---|---|---|
| Publish + Sync | ≥ %90 | Faz 3-4 içinde, eşzamanlı |
| Media + Auth | ≥ %85 | Faz 6-7 içinde |
| CMS CRUD | ~%60 | Faz 5 içinde |
| DTO / mapping | Hedef yok | — |

Sona bırakılan coverage süpürmesi, kolay kodu test ederek rakamı şişirmekle sonuçlanır.

---

## 11. Milestone'lar

Her milestone **durulabilir** ve **gösterilebilir** — yarım iş bırakmaz.

| MS | Ad | Kümülatif | Kanıt (demo edilebilir) | Kritik yolda? |
|---|---|---|---|---|
| **M0** | Green Build | 2,5 sa | `dotnet build` + `dotnet test` yeşil, Postgres ayakta | ✅ |
| **M1** | Persisted Domain | 9,5 sa | pgAdmin'de gerçek içerik ağacı, migration uygulanmış | ✅ |
| **M2** | **Walking Skeleton** | 22,0 sa | Swagger'dan login → token → Book CRUD; sync stub'ları canlı · **Mobil geliştirici başlayabilir** | ✅ |
| **M3** | **Publish Works** | 29,0 sa | v1 yayınla → içeriği düzenle → mobil hâlâ v1 görüyor → v2 yayınla · **En büyük risk kapandı** | ✅ |
| **M4** | **Sync Contract Live** | 35,5 sa | Gerçek delta; sözleşme dokümanı v1.0 teslim · **Kritik yol bitti** | ✅ |
| **M5** | CMS Complete | 42,5 sa | Admin dashboard tüm içerik ağacını yönetiyor | ✅ |
| **M6** | Media Live | 48,5 sa | Resim yükle → publish → mobil manifest'te checksum'lı URL | ✅ |
| **M7** | Secured | 52,5 sa | Refresh token, roller, lockout; yetkisiz erişim testleri geçiyor | ✅ |
| **M8** | **MVP Deployable** | 56,0 sa | `docker compose up` ile prod imajı ayakta, health yeşil | ✅ |
| **M9** | Hardened | 68,0 sa | ETag, cache, rollback, coverage, final review | ❌ |
| **M10** | Mobil & Web Uyumlu | 77,0 sa | Mobil/web geri bildirimindeki tüm additive düzeltmeler yayında | ❌ |

---

## 12. Risk Register

| # | Risk | Etki | Olasılık | Skor | Azaltma | Kapanış |
|---|---|---|---|---|---|---|
| 1 | **Delta sync semantik hatası** — mobilde eksik/bozuk içerik | 🔴 Kritik | Orta-Yüksek | **9** | Snapshot modeli (draft sızıntısı yapısal olarak imkânsız) · 7.5 test matrisi · fazı 8→4'e çekmek · stub ile şemayı erken dondurmak | M4 |
| 2 | **Mobil sözleşme uyumsuzluğu** — mobilci farklı varsaymış | 🔴 Yüksek | Yüksek | **9** | 5.0 stub endpoint · versiyonlanmış sözleşme dokümanı · şemayı implementasyondan önce dondurmak | M2 / M4 |
| 3 | **Media upload güvenlik açığı** — RCE / path traversal | 🔴 Kritik | Orta | **8** | Magic-byte + MIME allowlist · GUID isimlendirme · boyut limiti · `security-reviewer` zorunlu | M6 |
| 4 | **Tek geliştirici / scope creep / kesinti** | 🔴 Yüksek | Yüksek | **8** | Her milestone durulabilir · net MVP/defer ayrımı · %34 kesilebilir float havuzu | Sürekli |
| 5 | **Publication transaction tutarsızlığı** — yarım yayın | 🔴 Yüksek | Orta | **7** | Tek `IUnitOfWork` transaction · versiyon bump transaction içinde · idempotency testi | M3 |
| 6 | **Soft-delete query filter'ın yayın sorgularını bozması** | 🔴 Yüksek | Orta-Yüksek | **7** | Yayın tabloları filter dışında · `IgnoreQueryFilters` bilinçli · explicit tombstone testi | M3 |
| 7 | **jsonb / Npgsql serialization sürprizi** | 🟠 Yüksek | Orta | **6** | 2.4'te en erken doğrula · Book diliminde gerçek round-trip testi | M1-M2 |
| 8 | **.NET 10 paket uyumsuzluğu** | 🟠 Orta | Orta | **6** | 1.3'ü 1. saate al — hepsini kur ve derle · fallback `net9.0` | M0 |
| 9 | **JWT secret / config sızıntısı** | 🔴 Kritik | Düşük-Orta | **6** | Secret env var'dan · `.gitignore` 1. saatte · dev/prod ayrımı | M0 / M8 |
| 10 | **Snapshot payload boyutu patlaması** | 🟠 Orta | Orta | **5** | Medya payload dışında (sadece URL + checksum) · erken ölçüm · compression MVP'de | M8-M9 |

**Ek izleme (skor <5):** EF migration geri alınamazlığı · PG18 image davranış farkları · animasyon formatı belirsizliği.

**Risk kapanma eğrisi:** İlk 10 riskin skor toplamı 71. **M4'te (35,5 sa) bunun %62'si kapanıyor.**
Orijinal planda aynı oran ~45. saatte kapanıyordu — revizyonun asıl kazancı bu.

---

## 13. Uygulama Sırası

### İlk gün (0 → 9,5 sa) — Zemini kur, sürprizleri erken çıkar

`1.1 → 1.2 → 1.3 → 1.5 → 2.1 → 2.2 → 2.3 → 2.4 → 2.5 → 2.6 → 2.7`

Sabah build'i yeşile çevir (`ApplicationUser.cs` boş, solution derlenmiyor) ve **tüm NuGet
paketlerini kurup derle** — .NET 10 uyum sürprizini 1. saatte gör. Öğleden sonra domain modelini
bitir, migration'ı uygula, **gerçekçi hacimde seed veri** üret. Seed "nice to have" değil, kritik
yol enabler'ı.

### İkinci aşama (9,5 → 22 sa) — Tek dikey dilim + sözleşmeyi dondur

`3.1 → 3.2 → 3.3 → 6.1 → 4.1 → 4.2 → 4.3 → 4.4 → 4.5 → 5.0`

Sadece **Book** üzerinden tüm stack'i uçtan uca kanıtla. **Auth pipeline'ını ve test token
helper'ını bu aşamada kur** — sonradan gelirse her entegrasyon testinin kurulumu elden geçer.
Son 1,5 saatte sync şemasını dondur ve stub endpoint'leri aç → mobilci başlar.

### Üçüncü aşama (22 → 35,5 sa) — En riskli işi burada bitir

`6.2 → 6.3 → 6.4 → 6.5 → 6.6 → 7.1 → 7.2 → 7.3 → 7.4 → 7.5 → 7.6`

Bu aşamanın **yaklaşık yarısını teste ayır** ve bu oranı pazarlık konusu yapma. Zorunlu senaryolar:
draft sızmıyor mu · v1→v2 delta doğru mu · silinen tombstone geliyor mu · publish idempotent mi ·
soft-delete filter'ı yayın sorgusunu bozuyor mu.

### Kritik yolu bozmadan paralel yürütülecekler (35,5 → 56 sa)

```
WS-B  CMS Completion    8.1 → 8.6    7,0 sa   Book desenini çoğalt
WS-E  Media Pipeline   10.1 → 10.6   6,0 sa   Tam bağımsız · security-reviewer zorunlu
WS-D  Auth Feature Set  9.1 → 9.4    4,0 sa   Çekirdek zaten M2'de
```

Sıra: önce **CMS** (ürünün kullanılabilirliği), sonra **Media**, en son **Auth feature**.
Zaman daralırsa kesme sırası tam tersi.

### Kapanış (56 sa) — MVP sınırı

`11.1 → 11.2 → 11.3 → 11.4` ile dur ve **M8'i teslim edilebilir nokta ilan et.** Bu 3,5 saati
"hardening" kutusuna atıp ertelemek en sık yapılan hata — zaman daralınca kutunun tamamı atlanır
ve deploy edilemeyen bir sistem kalır.

---

## 14. İlerleme Takibi

### PHASE 0 — Foundation & Green Build → M0

- [x] 1.1 Git init + `.gitignore` + `obj`/`bin` temizliği
- [x] 1.6 `global.json` + GitHub Actions CI + remote → ilk push (CI kırmızı görüldü)
- [x] 1.2 `ApplicationUser` + `Class1.cs` temizliği + `Directory.Build.props` + `.editorconfig` (CI yeşile döndü — PR #1)
- [x] 1.3 NuGet paketleri + .NET 10 uyum doğrulaması → **Risk #8 kapandı**
- [x] 1.4 Test projesi (`tests/Isbak_SAR_Guide.Tests`) + ilk 2 test geçti
- [x] 1.5 `docker compose up` + PG18 bağlantı doğrulaması

**Faz 0 kurulan altyapı:**

| Karar | Seçim | Not |
|---|---|---|
| SDK kilidi | `global.json` → 10.0.400, `rollForward: latestFeature` | Yerel/CI sürüm kayması önlendi |
| Ortak csproj ayarları | `Directory.Build.props` | `TreatWarningsAsErrors` açık |
| Mapping | Mapster 10.0.12 | |
| Validation | FluentValidation 12.1.1 | |
| Logging | Serilog.AspNetCore 10.0.0 | |
| API versiyonlama | Asp.Versioning.Mvc.ApiExplorer 10.2.1 | |
| OpenAPI UI | Scalar.AspNetCore 2.17.1 | `Microsoft.AspNetCore.OpenApi` ile birlikte; Swagger UI klasik alternatif |
| Test | xUnit 2.9.3 + Shouldly + NSubstitute + Testcontainers.PostgreSql | |
| Identity | `ApplicationUser : IdentityUser`, `Id` = string/GUID, giriş `UserName` ile | `Microsoft.Extensions.Identity.Stores` — EF Core sürüklemiyor |

> **Kural dosyasından iki sapma:** (1) FluentAssertions yerine **Shouldly** — v8+ ticari lisans gerektiriyor
> (nuget sayfasından doğrula). (2) Moq yerine **NSubstitute** — Moq 4.20 SponsorLink olayı sonrası.
> Her ikisi de tek satırla geri alınabilir.

### PHASE 1 — Domain, Persistence & Seed → M1 ✅ Tamamlandı

- [x] 2.1 `BaseEntity` + soft-delete alanları (`IsDeleted`, `DeletedAt`)
- [x] 2.2 İçerik entity'leri tamamlandı (`Slug`, `LanguageCode`, `Checksum`, `DataJson`)
- [x] 2.3 `BookPublication` + `PublishedContent` (immutable, `BaseEntity`'den türemiyor)
- [x] 2.4 6 `IEntityTypeConfiguration<T>` + jsonb + index'ler — Risk #6 canlı yakalanıp `IsRequired(false)` ile düzeltildi
- [x] 2.5 `DbContext` + `SaveChanges` override'ında merkezi audit damgalama
- [x] 2.6 İlk migration → Postgres'e uygulandı (14 tablo doğrulandı)
- [x] 2.7 Seed: roller (Admin/Editor), admin kullanıcı, 1 kitap / 4 modül / 16 içerik / 17 blok

### PHASE 2 — Walking Skeleton + Contract Stub → M2

- [x] 3.1 DI kompozisyonu (`AddDataAccess`/`AddBusiness`, katman sızıntısı düzeltildi)
- [x] 3.2 `Result<T>` + `Error`
- [x] 3.3 `GlobalExceptionHandler` (IExceptionHandler) + `ResultExtensions.ToActionResult()`
- [x] 3.4 Serilog
- [x] 3.5 API versioning (Asp.Versioning) + Scalar UI + CORS
- [x] 6.1 Auth çekirdeği — 5 canlı HTTP testiyle doğrulandı (401 → login → 200 → yanlış şifre → olmayan kullanıcı, enumeration koruması dahil)
- [x] 4.1 `IRepository<T>` + `EfRepository<T>` + `IUnitOfWork`
- [x] 4.2 Book DTO (`Dtos/`) + Mapster + Validation (`Validators/`)
- [x] 4.3 `BookService` (validation dahil)
- [x] 4.4 `BooksController` — 8 canlı HTTP testiyle doğrulandı (CRUD + validation + soft-delete)
- [x] 4.5 Entegrasyon test harness'ı — Testcontainers + `WebApplicationFactory`, 6/6 test geçti (unit + smoke + auth akışı)
- [x] 5.0 Sync contract stub — `manifest`/`snapshot`/`changes` (`[AllowAnonymous]`), gerçek seed verisiyle 4 canlı testle doğrulandı; `changes` bilerek stub (Faz 4'te gerçek delta)

### PHASE 3 — Publishing Engine → M3

- [x] 6.2 Snapshot tasarımı

> **6.2 tasarım kararları (2026-08-25):**
> 1. **`PublishedContent.PayloadJson` = `SyncContentDto` JSON'ı, aynen.** Publish anında her content
>    (Blocks + Media özeti dahil) 5.0'da donmuş `SyncContentDto` şemasıyla serialize edilip yazılır.
>    Faz 4'te snapshot/delta bu kolonu deserialize etmeden doğrudan mobile geçirir; ikinci bir şema
>    ve dönüşüm katmanı yok (YAGNI). `ModuleId` DTO'da zaten mevcut.
> 2. **`BookPublication.ManifestJson` publish transaction'ında dondurulur.** Versiyon, `PublishedAt`,
>    içerik sayısı, medya listesi ve checksum publish anında hesaplanıp yazılır. Yayın gerçekten
>    immutable: admin draft'ı sonra değiştirse bile yayınlanmış manifest değişmez. Sync sadece okur.
> 3. **Deterministik sıralama DTO kurulurken C# tarafında garanti edilir:** Modules/Contents/Blocks
>    serialize edilmeden önce `OrderBy(DisplayOrder).ThenBy(Id)`. Checksum idempotency'si (6.5) buna
>    dayanır; garanti sorguda değil, serileştirmenin yanında durur ki sorgu değişse de bozulmasın.
> 4. **`PayloadJson`/`ManifestJson` kolonları `json` (`jsonb` değil):** checksum invariant'ı
>    (`Checksum = SHA256(PayloadJson)`, tombstone dahil) bayt sadakati gerektirir; `jsonb` metni
>    kanonikleştirir (key sıralar, whitespace atar) ve invariant'ı DB'den geri okuyunca bozar.
>    `json` metni aynen saklar + geçerlilik doğrular. 6.5 invariant testi yakaladı (2026-08-25).
>    Draft tarafındaki `ContentBlock.DataJson` bilerek `jsonb` kalır — yapısal veri, checksum sözü yok.
> 5. **Kanonik form = wire format:** `PropertyNamingPolicy = CamelCase` (5.0 wire sözleşmesiyle hizalı,
>    Faz 4 verbatim geçişin ön şartı) + `UnsafeRelaxedJsonEscaping` (Türkçe karakterler `\uXXXX` değil
>    UTF-8; bu JSON asla HTML'e ham gömülmez). Kanonik options **donmuştur** — her değişiklik
>    yayınlanmış tüm checksum'ları geçersiz kılar.
>
> **Not — web okuyucu:** Kitapçık ileride halka açık web'de de yayınlanacak. Bu tasarımı değiştirmez:
> web okuyucu, mobil gibi aynı immutable yayın tablolarından beslenen ikinci bir tüketicidir.
> Gerekirse ileriki fazlarda ayrı bir public web read API görevi açılır (backlog).

- [x] 6.3 `IPublishingService`
- [x] 6.4 Tombstone
- [x] 6.5 Publish senaryo testleri
- [x] 6.6 Publish endpoint

### PHASE 4 — Synchronization → M4

> **Faz 4 öncesi tasarım notları (2026-08-25 — web/mobil ilk tasarımlar incelendi):**
> 1. **Public web read API — yeni görev bloğu gerekiyor.** Son kullanıcı kitabı web'de de okuyacak.
>    Web, sync endpoint'lerini KULLANMAZ (tam paket indirmek web için israf); kendi per-resource
>    uçlarını alır (TOC + içerik detayı), ama aynı yayın tablolarından okur — asla draft'tan.
> 2. **`GetSnapshot`/TOC kaynağı sorunu:** Yayın tablolarında modül ağacı ve kitap metadata'sı
>    şu an DONDURULMUYOR (sadece content payload'ları + manifest var). 7.2 tam paketi ve web TOC'u
>    bunlara muhtaç — Faz 4 tasarımında çözülmeli (aday: publish'te SnapshotJson kolonu da dondur).
>    **✔ Çözüldü (2026-08-25):** `BookPublication.SnapshotJson` kolonu (`json`, NOT NULL) — tam kanonik
>    snapshot publish'te dondurulur, `GetSnapshot` deserialize etmeden aynen döner. İnvariant genişledi:
>    `Checksum = SHA256(SnapshotJson)`. Tek-serialize kuralı: `BuildManifest` checksum'ı parametre alır.
>    Bilinçli veri tekrarı (satırlar delta'nın, snapshot ilk kurulumun kaynağı — immutable, drift edemez).
> 3. **Web frontend sözleşme uyumsuzluğu:** Mock, içerik gövdesini tek markdown string tutuyor;
>    backend sözleşmesi yapısal ContentBlock listesi. Web'ciyle hizalanmalı (öneri: frontend block
>    render eder; backend markdown'a çevirmez — iki format = drift).

- [x] 7.1 Manifest
- [x] 7.2 Snapshot
- [x] 7.3 Changes (delta) — 7.3-a: publish journal modeline geçti (Faz 3 tadilatı, satır tablosu artık degisiklik günlüğü); 7.3-b: sözleşmeye `Modules` eklendi; 7.3-c: gerçek delta motoru (`SyncChangesJsonWriter`, verbatim envelope)
- [x] 7.4 `SyncController` — 7.1-7.3 ile birlikte gerçek okumalara bağlandı
- [x] 7.5 Delta test matrisi — 15 senaryo, 7.3-c ile birlikte yazıldı
- [x] 7.6 Sözleşme dokümanı v1.0 — `docs/Sync-Sozlesmesi-v1.md`, tüm örnekler gerçek API yanıtlarından

### PHASE 5-8 — CMS / Media / Auth / Release → M5-M8

- [x] 8.1-8.6 CMS Completion — Module/Content/ContentBlock CRUD + reorder (`ReorderHelper`) + paging (`PagedResult<T>`), `feature/phase5-cms-completion` (PR #9)
- [x] 10.1-10.6 Media Pipeline — `IStorageService`/`LocalFileStorageService`, magic-byte upload validation, dedup, orphan cleanup, `feature/phase6-media-pipeline` (PR #10)
- [x] 9.1-9.4 Auth Feature Set — refresh token rotation + reuse detection, Admin-provisions-Editor, lockout, login rate limiting, `feature/phase7-auth-feature-set` (PR #11)
- [x] 11.1-11.4 Release Readiness — health check'ler (`/health` liveness, `/health/ready` readiness), güvenlik başlıkları (`Response.OnStarting` ile 500'lerde de garanti), HSTS, response compression, `Dockerfile`/`compose.prod.yaml`/`docs/Deployment.md`, ghcr.io CD, `feature/phase8-release-readiness` (PR #13) — gerçek `docker compose up` ile doğrulandı, review 2 gerçek bug buldu (storage yazma izni, container-içi curl eksikliği)

> Ara adım (Faz 8 öncesi, `fix/architecture-review-findings`, PR #12): 5 paralel uzman ajan +
> graphify bağımlılık grafiğiyle mimari inceleme — katmanlama temiz çıktı, 9 gerçek bulgu
> düzeltildi (UserService rol-atama rollback'i, Media orphan-file riski, `AddBusiness()`'ın
> JwtOptions için kendi kendine yeterli olması, 12x doğrulama-hata tekrarının
> `ValidationResultExtensions`'a çıkarılması, `PublishAsync` bölünmesi, yarış-durumu `catch`
> bloklarına loglama). Detay: CLAUDE.md ilgili bölümleri.

### PHASE 9 — Hardening → M9

- [x] 12.1 Payload boyut ölçümü — gerçek seed kitabına (Book id=1, v16, 97 content) karşı
      ölçüldü: manifest 16,8 KB ham / ~6,1 KB gzip; tam snapshot 139,6 KB ham / ~42,8 KB
      gzip; gerçekçi bir delta (v15→v16, tek publish sonrası) sadece 1,8 KB. Sunucu-içi
      yanıt süresi 5-115 ms arası (çoğu <15ms). **Sonuç: 12.2 (cache) YAGNI — ölçüm
      cache'i gerektirmiyor**, snapshot zaten küçük ve response compression (Faz 8) tek
      başına yeterli; en gerçekçi mobil senaryo olan delta-sync zaten KB mertebesinde.
      Cache eklemek şu an spekülatif optimizasyon olurdu (roadmap'in kendi YAGNI
      gerekçesiyle tutarlı) — ölçüm rakamları değişirse (çok daha büyük bir kitap/çok
      daha yüksek trafik) yeniden değerlendirilebilir.
- [x] 12.3 `AsNoTracking` / projeksiyon denetimi, N+1 avı — N+1 bulunmadı
      (`GetWithFullTreeAsync` zaten tum agaci tek sorguda Include/ThenInclude ile
      eager-load ediyor, `SnapshotBuilder` sadece bellek-ici mapping yapiyor).
      `AsNoTracking()` sadece salt-okunur oldugu tek tek dogrulanan sorgulara
      eklendi (`FindAllAsync`, `GetPagedAsync`'ler, sibling `FindAllByXAsync`'ler,
      Media dedup/orphan sorgulari) — `FindByIdAsync`, `RefreshTokenRepository.
      FindByTokenHashAsync` (dogrudan property mutation) ve `GetWithFullTreeAsync`
      (Book kok'u Version bump ile mutate ediliyor) bilinçli olarak tracked
      birakildi. `PublicationRepository`'nin manifest/snapshot/changes sorgulari
      zaten `Select()` projeksiyonu ile örtük olarak tracking-disi. 139 test yeşil.
- [x] 12.5 Coverage denetimi + eksik test tamamlama — `dotnet test --collect:"XPlat
      Code Coverage"` (coverlet) çalıştırıldı, cobertura raporu ayrıştırıldı. Genel
      satır oranı zaten %89,9 (migration/OpenAPI generated kod hariç tutulunca gerçek
      resim netleşti) ama gerçek bir sıfır-kapsam bulundu: **`BookService`'in kendi
      CRUD'u (Create/Update/Delete/GetById) hiçbir testte hiç çağrılmamış** — Module/
      Content/ContentBlock testleri hep `unitOfWork.Books.AddAsync` ile doğrudan test
      kitabı açıyor, `BookService`'i baypas ediyor. `BookServiceTests.cs` eklendi.
      Bu testi yazarken gerçek bir bug ortaya çıktı: `BookService.CreateAsync`/
      `UpdateAsync`'te `Book.Slug` unique index ihlali hiç yakalanmıyordu (Media/
      PublishingService'teki aynı desenin aksine) — tekrar eden slug 500'e düşerdi,
      düzeltildi (409 Conflict). Ayrıca `GlobalExceptionHandler.TryHandleAsync`
      (tek global beklenmedik-hata yakalama noktası) ve `LoginDtoValidator` da hiç
      test edilmiyordu — ikisi için de doğrudan unit test eklendi (`tests/.../Unit/`).
      156/156 test yeşil (139 mevcut + 17 yeni).
- [x] 12.6 Rollback / restore endpoint'i — `POST /api/v1/books/{bookId}/rollback`
      (Admin-only, `{toVersion}`). Publication modeli immutable oldugu icin "rollback"
      eski bir satiri degistirmek degil, o versiyonun zaten saklanmis `SnapshotJson`'ini
      YENI bir versiyon olarak tekrar yayinlamak (git revert deseni) — CMS draft agacina
      hic dokunmuyor, sadece mobilin gordugu yayin gecmisini etkiliyor. `PublishAsync`'in
      paylasilan statik yardimcilarini (`BuildPublicationShell`/`AppendChangedContents`/
      `AppendTombstones`) degistirmeden yeniden kullaniyor — `PublishAsync`'in kendisi
      dokunulmadan %100 coverage'da kaldi. `toVersion >= mevcut en son versiyon` Validation,
      var olmayan versiyon NotFound. Gercek seed kitaba (v16 → v15'e rollback → v17, 98
      gercek content) karsi da dogrulandi — manifest/snapshot checksum invariant'i tutuyor,
      draft agac degismedigi teyit edildi. 166/166 test yeşil (13 yeni).
- [x] 12.8 Global rate limiting — `GlobalRateLimitOptions` (300/60s varsayılan, IP başına),
      TÜM endpoint'lere `AddRateLimiter`'ın `GlobalLimiter`'ı ile otomatik uygulanıyor
      (named "login" politikasının aksine opt-in gerekmez, ikisi TOPLANIR). `/health` ve
      `/health/ready` bilinçli olarak `DisableRateLimiting()` ile muaf — canlı doğrulamada
      (PermitLimit=3 ile manuel test) muafiyet olmadan health check'in de 429 döndüğü
      görüldü, bu orkestratörün uygulamayı "ölü" sanıp gereksiz yeniden başlatmasına yol
      açardı. `compose.prod.yaml`/`.env.example`/`docs/Deployment.md`'ye işlendi.
- 12.2 (cache), 12.4 (ETag), 12.7 (WebP), 12.9 (Public read) — roadmap'in kendi ön
      koşulları (ölçüm/mobil stabilite/lisans/müşteri talebi) karşılanmadığı için bu
      fazda bilinçli olarak ERTELENDİ, görev-görev karar verildi.
- [x] 12.7 (WebP + thumbnail) — yeniden gündeme geldi (2026-08-31, mobil optimizasyon
      sorusu), kullanıcı onayıyla **artık ERTELENMİYOR, uygulandı** (iki ön koşul —
      medya hacmi artışı, lisans doğrulaması — resmi tetiklenme şartı olmaktan çıktı,
      doğrudan yapıldı). `SixLabors.ImageSharp`'ın Split License'ı yerine `SkiaSharp`
      (MIT) seçildi — ticari kullanımda kısıtlama yok. `MediaService.UploadAsync`
      artık her yüklenen görseli SkiaSharp ile decode edip **storage'a yazılan asıl
      dosyayı WebP'ye çeviriyor** (`ContentType` her zaman `image/webp`, orijinal
      format ne olursa olsun) + `Media.ThumbnailStoragePath` (yeni, nullable kolon,
      migration `AddMediaThumbnailStoragePath`) altında `Storage:ThumbnailMaxDimension`
      (varsayılan 400px) ile sınırlı bir küçük önizleme üretiyor. `Checksum` artık
      WebP-sonrası baytlardan hesaplanıyor (dedup + mobil bütünlük doğrulaması **tek
      alanla** çalışmaya devam ediyor — WebP encode deterministik olduğu için ayrı bir
      "orijinal" checksum alanına gerek çıkmadı, ilk tasarım varsayımı yanlıştı).
      Sync sözleşmesine additive `MediaSummaryDto.ThumbnailUrl` eklendi (Faz 10'daki
      `book`/`ContentCount` ile aynı "trailing nullable param" deseni). **Sadece bu
      özellikten SONRAKİ yüklemeler** — mevcut 93 medyaya geriye dönük backfill
      YAPILMADI, `ThumbnailStoragePath` o satırlarda `null` kalıyor.
      **Kod incelemesinde bulunan gerçek bir hata:** `SKBitmap.Decode`, imza doğru
      ama gövdesi bozuk bir dosyada (`ImageSignatureDetectorTests.BuildMinimalPng`
      gibi sahte-header-gerçek-piksel-yok girdilerde) beklenenin aksine `null` değil
      `ArgumentNullException` fırlatıyordu — yakalanmasaydı saldırgan kontrollü böyle
      bir dosya `400 Validation` yerine `500`'e düşerdi; dar bir try/catch ile
      düzeltildi, doğrudan regresyon testi eklendi. Docker: `SkiaSharp.NativeAssets.
      Linux` API projesine eklendi, `Dockerfile`'a `libfontconfig1` (SkiaSharp'ın
      Linux'taki bilinen bağımlılığı — Faz 8'deki curl/`/storage` izin sınıfı bir
      "sadece `docker compose up`'ta ortaya çıkar" riski, henüz canlı compose ile
      doğrulanmadı, birleştirmeden önce önerilir). 208/208 test yeşil.

### PHASE 10 — Mobil & Web Uyumluluk Düzeltmeleri → M10

Kaynak: `docs/mobil_ekip_geri_bildirim_v1.1.md` (4 madde) +
`docs/Web-Frontend-Geri-Bildirim-v2.md` / `docs/Frontend-Notlar-ve-Oneriler.md`
(8 madde) — iki takımın gerçek çalışan backend'e karşı entegrasyon sırasında
bulduğu, sözleşmeyi bozmayan boşluk/uyumsuzluklar. Detaylı gerekçe/tasarım
her alt görevin kendi commit mesajında ve §5.13'teki WBS tablosunda.

- [x] 13.1 Sync/CMS sözleşme netleştirmeleri — medya base URL (aynı host, `/api/v{version}`
      öneki YOK), varyant grubunun üst listede hangi title/summary'yi göstereceği (en küçük
      `displayOrder`'lı varyant kazanır), `POST /auth/login`'in 401'inin (diğer uçların aksine)
      dolu ProblemDetails döndüğü, CORS'un zaten config-driven olduğu (`Cors:AllowedOrigins`/
      `CORS_ALLOWED_ORIGIN_0..`) — dördü de dokümantasyon-only, kod zaten doğru davranıyordu.
- [x] 13.2 `/sync/changes`'e `book` alanı ekle — `Modules`'la ayni gerekce ve
      desen (7.3-b), koşulsuz her yanıtta gelir. Örneği canlı v17→v18 verisine
      karşı yeniden üretirken keşfedilen bir yan bulgu: dev DB'de Faz 6'dan kalma
      test verisi (Module 13/Content 100/Media 94) gerçek kitaba karışmış,
      contentCount'u 97 yerine 98 gösteriyordu — temizlenip yeniden yayınlandı
      (v18), gerçek sayı 97'ye döndü.
- [x] 13.3 Publish `IsPublished`'a göre süzsün + tek seferlik backfill + `Book.IsPublished`
      bugfix'i — canlı doğrulama önce yapıldı: 8/10 gerçek Modül ve 85/97 gerçek Content
      `IsPublished=false`'du (mobile'a zaten servis edilmesine rağmen) — filtre kod
      değişikliğinden ÖNCE kapsamlı bir SQL backfill (sadece Book id=1, silinmemiş satırlar)
      ile hepsi `true` yapıldı, yoksa bir sonraki publish 85 gerçek content'i sessizce
      tombstone'lardı. `SnapshotBuilder.BuildSnapshot` artık Module/Content'i kendi
      `IsPublished` bayrağına göre süzüyor — mevcut `AppendTombstones` mekanizması özel kod
      gerekmeden bunu otomatik tombstone'lıyor. Yan bulgu: `Book.IsPublished` hiçbir yerde
      set edilmiyordu (19+ gerçek yayından sonra bile hep varsayılan false) — `PublishAsync`
      artık `book.Version` ile birlikte bunu da `true` yapıyor. Gerçek kitaba karşı doğrulandı:
      backfill sonrası yeniden yayında contentCount hâlâ tam 97 (regresyon yok). 170/170 test
      yeşil (4 yeni).
- [x] 13.4 `ModuleDto.ContentCount` (admin panel N+1 düzeltmesi) — `IModuleRepository.GetPagedAsync`
      artık `ModuleWithContentCount` (`Module` + `Contents.Count(!IsDeleted)`) döner, tek sorguda
      hesaplanır; `ModuleDto`'ya additive `ContentCount = 0` alanı eklendi (sadece bu liste ucu
      doldurur, tekil/create/update/reorder'da hep `0`). 171/171 test yeşil (1 yeni:
      soft-delete edilmiş content'in sayılmadığını doğruluyor). `docs/CMS-API-Sozlesmesi-v1.md`
      Module liste yanıtı güncellendi.
- [x] 13.5 Reorder'ın alakasız bloğun `dataJson`'ını bozmasını önle — `ReorderHelper`'ın
      `markDirty`'si artık `IRepository<T>.Update(entity)` (tüm kolonları kirli işaretler)
      yerine yeni `IRepository<T>.UpdateProperty(entity, x => x.DisplayOrder)`
      (`EfRepository<T>`, `dbContext.Entry(entity).Property(...).IsModified = true`) kullanıyor
      — UPDATE artık sadece `DisplayOrder` (+ audit `UpdatedAt`) kolonunu kapsıyor,
      `ContentBlock.DataJson` (jsonb) hiç dokunulmuyor. Regresyon testi yazarken bir yan bulgu:
      Postgres jsonb kolonu zaten İLK INSERT'te kendi kanonik biçimine dönüştürüyor (anahtar
      sırası uzunluğa göre değişiyor, örn. `{"headers":...,"rows":...}` → `{"rows":...,
      "headers":...}`) — bu yüzden test, orijinal gönderilen string'e değil, reorder ÖNCESİ
      DB'den okunan kanonik değere karşı reorder SONRASI değeri karşılaştırıyor (tam eşleşme
      bekleniyor, taşınan blok dahil). 172/172 test yeşil (1 yeni:
      `ReorderAsync_DoesNotAlterSiblingsDataJson`).
- [x] 13.6 Tam `/users` CRUD (liste, rol değiştirme, pasifleştirme, kendi şifresini değiştirme) —
      `IUserService`'e `GetAllAsync`/`ChangeRoleAsync`/`DeactivateAsync`/`ChangeOwnPasswordAsync`
      eklendi (`UserManager<ApplicationUser>` üzerinden, aynı `CreateAsync` deseni). Deaktivasyon
      hard delete değil — `SetLockoutEndDateAsync(..., DateTimeOffset.MaxValue)`, FK bütünlüğünü
      bozmaz; bir Admin kendi hesabını kilitleyemez (self-lockout guard, `400 Validation`).
      **Auth tasarımı canlı testle düzeltildi:** ilk deneme sınıf seviyesine
      `[Authorize(Roles = "Admin")]` koyup `PUT /users/me/password`'e sadece `[Authorize]`
      eklemekti ("eylem seviyesi sınıfı geçersiz kılar" varsayımıyla) — bir Editor token'ıyla
      canlı HTTP testi `403` döndürdü, çünkü ASP.NET Core çoklu `[Authorize]` filtrelerini
      **birleştirir** (AND), en yakını kazanmaz. Düzeltme: sınıf seviyesi sadece `[Authorize]`,
      Admin-only dört eylemin (Create/GetAll/ChangeRole/Deactivate) her biri kendi
      `[Authorize(Roles = "Admin")]`'ini taşıyor, `me/password` ek kısıt taşımıyor.

      **Faz sonu parallel security-reviewer + csharp-reviewer geçişi bir CRITICAL + bir HIGH +
      üç MEDIUM bulgu çıkardı, hepsi bu görev bitmeden düzeltildi:**
      - **CRITICAL** — `DeactivateAsync` sadece `SetLockoutEndDateAsync` çağırıyordu;
        `IsLockedOutAsync` sadece `LoginAsync`'te kontrol ediliyordu, `RefreshAsync`'te değil —
        yani zaten alınmış bir refresh token pasifleştirmeden SONRA bile rotasyonla süresiz
        yenilenebiliyordu (deaktivasyon erişimi gerçekte KESMİYORDU). Düzeltme:
        `DeactivateAsync`/`ChangeOwnPasswordAsync` artık `unitOfWork.RefreshTokens.
        RevokeAllActiveForUserAsync` çağırıyor (`AuthService`'in reuse-tespitinde kullandığı
        aynı metod); `AuthService.RefreshAsync`'e de defense-in-depth olarak `IsLockedOutAsync`
        kontrolü eklendi.
      - **HIGH** — `ChangeRoleAsync` önce TÜM mevcut rolleri kaldırıp sonra yeni rolü ekliyordu;
        `UserManager` her çağrıyı ayrı/anında commit ettiği için (tek transaction değil) ikinci
        adım başarısız olursa kullanıcı kalıcı olarak rolsüz kalabilirdi. Düzeltme: sıra
        tersine çevrildi (önce ekle, sonra kaldır) — ara başarısızlık kullanıcıyı rolsüz değil
        fazla-rollü bırakır.
      - **MEDIUM** — Sistemdeki son Admin'i `ChangeRoleAsync` ile düşürmek ya da `DeactivateAsync`
        ile pasifleştirmek mümkündü (kurtarılamaz kilitlenme, self-lockout guard'ın önlediği
        AYNI senaryo ama başka bir yoldan) — her iki metoda da `GetUsersInRoleAsync(Admin).Count
        <= 1` kontrolü eklendi (`User.LastAdminProtected`). **Not:** bu sınırı gerçek bir
        entegrasyon testiyle kanıtlamak, paylaşılan seed `admin` hesabının rolünü değiştirmeyi
        gerektirirdi — testler bunu yapmamalı (bkz. CLAUDE.md "Testing": "don't mutate the
        shared seed ... admin user", düzinelerce başka test buna güveniyor) — bu yüzden bilerek
        sadece kod incelemesiyle doğrulandı, otomatik testi yok.
      - **MEDIUM** — `IdentityResult` hata mesajı birleştirme deseni (`string.Join("; ",
        ...Errors.Select(...))`) `UserService.cs` içinde 6 kez tekrarlanmıştı — yeni
        `Business/Common/IdentityResultExtensions.cs` (`ValidationResultExtensions`'ın
        FluentValidation-dışı eşdeğeri) tüm çağrı noktalarına uygulandı.
      - **MEDIUM** — `ChangeRole` (ayrıcalık yükseltme riski taşıyan en hassas eylem) HTTP
        seviyesinde 403 testi eksikti — `ChangeRole_WithEditorToken_ReturnsForbidden` eklendi.

      Düzeltmeler sırasında bir test-özel EF Core tuzağı da bulundu: `RevokeAllActiveForUserAsync`
      (`ExecuteUpdateAsync`, change tracker'ı atlar) + aynı DbContext scope'unda önceden tracked
      edilmiş bir `RefreshToken` + hemen ardından aynı scope'ta tracked bir sorgu (`FindByTokenHashAsync`)
      birleşince, entity'nin bellekteki (stale) hâli döner — EF'in identity resolution'ı. Gerçek
      üretimde her HTTP isteği ayrı bir scope aldığı için bu oluşmaz; testler bu yüzden
      login/revoke/refresh adımlarını AYRI `CreateScope()` bloklarında çalıştırıyor (gerçek
      istek sınırlarını taklit eder) — tek bir paylaşılan scope kullanan ilk hâli yanlışlıkla
      yeşil çıkabiliyordu (bkz. `DeactivateAsync_RevokesTargetUsersActiveRefreshToken`'ın kod
      yorumu).

      187/187 test yeşil (15 yeni: 8 servis + 1 HTTP-seviyesi ilk turdan, +2 servis regresyon
      testi + 1 eksik HTTP-seviyesi testi review sonrası eklendi). `docs/CMS-API-Sozlesmesi-v1.md`
      §3.5 dört yeni uç + auth-tasarım notu + son-Admin/refresh-token-iptal davranışlarıyla
      güncellendi. `pageSize` üst sınırı olmaması bilerek kapsam dışı bırakıldı (LOW,
      Modules/Contents/ContentBlocks'ta da aynı ön-var-olan desen, bu branch'in dışında).
- [x] 13.7 Video/Animation `dataJson` taslak şeması (provisional) — doküman-only, kod
      değişikliği yok (`ContentBlock.DataJson` zaten şemasız arbitrary JSON kabul ediyor,
      Table/Warning'le aynı). `docs/Sync-Sozlesmesi-v2.md` §4.1/§4.2'ye eklendi, açıkça
      "PROVISIONAL — henüz gerçek içerik yok" uyarısıyla işaretli: Video için mevcut `media`
      alanı yeterli, sadece elle seçilmiş kapak görseli gerekirse `dataJson.thumbnailMediaId`;
      Animation için `dataJson.steps` dizisi (her adım kendi `text` + opsiyonel `mediaId`'si).
      Zamanlama/süre bilgisi bilerek dışarıda bırakıldı (YAGNI, gerçek ihtiyaç yok).
- 13.8 Acil Durum Bandı (web #1) — backend desteği (Book'a alan + Admin PUT + manifest'e
      ek alan ya da yeni anonim endpoint) ERTELENDİ, kullanıcı onayıyla: şu an öncelik değil,
      sonradan eklenebilir additive bir özellik.
- [x] 13.10 (2026-08-31, web'in "bazen oturum süresi doldu diyor, elle çıkış yapmam gerekiyor"
      geri bildirimi) — **backend tarafı düzeltildi: rotasyona kısa bir grace window eklendi.**
      Kök neden hipotezi doğrulanmadan (log/gerçek kullanım kanıtı olmadan) ama zararsız ve
      geriye dönük güvenli olduğu için uygulandı: `AuthService.RefreshAsync`'in tek kullanımlık
      rotasyonu, zaten iptal edilmiş bir token tekrar sunulduğunda artık koşulsuz "hırsızlık"
      saymıyor — `RefreshToken.RevokedByRotation` (yeni kolon, migration
      `AddRefreshTokenRevokedByRotation`) bu iptalin rotasyondan mı (grace window uygulanabilir)
      yoksa açık logout/toplu iptalden mi (`RevokeAsync`, `RevokeAllActiveForUserAsync` —
      deaktivasyon/reuse-cezası) geldiğini ayırt ediyor; **sadece rotasyon kaynaklı ve
      `Jwt:RefreshTokenRotationGraceSeconds` (varsayılan 10 sn) içindeki** iptaller için yeni bir
      çift üretilip kullanıcı zorla logout edilmiyor — açık logout'tan hemen sonra aynı token'la
      "tekrar giriş" gibi bir güvenlik açığı oluşmuyor (grace window'un bilerek rotasyona özel
      tutulma gerekçesi). Grace window dışında ya da rotasyon-dışı bir iptalse davranış
      değişmedi: tüm aktif token'lar iptal edilir. Mevcut iki reuse testi
      (`AuthServiceTests`/`AuthTests`) yeni davranışa göre güncellendi + iki yeni test eklendi
      (grace window içinde başarı, grace window dışında hâlâ toplu iptal — zaman aşımını
      gerçekten beklemek yerine `RevokedAtUtc`'yi geriye alarak deterministik simüle edilir).
      206/206 test yeşil. **Hâlâ doğrulanmadı/tamamlanmadı:** web dashboard'ın refresh
      çağrılarını tek-uçuşlu (single-flight) yapıp yapmadığı ve otomatik 401→login
      yönlendirmesi olup olmadığı — bkz. `Frontend-Notlar-ve-Oneriler.md` madde 10, bu backend
      repo'sunun dışında, web ekibinin kendi kontrol etmesi gerekiyor.
