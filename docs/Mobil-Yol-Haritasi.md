# Isbak SAR Guide — Mobil Uygulama Yol Haritası

Kentsel arama-kurtarma el kitabının Flutter tabanlı, tamamen offline mobil okuyucusu için
mimari kararlar, iş kırılımı ve uygulama sırası. Bu doküman **plan**dır — kod bu backend
reposunda yaşamaz, ayrı bir Flutter reposunda geliştirilir. Backend tarafındaki donmuş
sözleşme `docs/Sync-Sozlesmesi-v1.md`'dir; bu roadmap o sözleşmeyi **tüketen** tarafın planıdır,
sözleşmeyi değiştirmez.

**Durum:** Backend hazır — `manifest`/`snapshot`/`changes` canlı, v1.1 sözleşme donuk, kitabın
tamamı (10 modül, 97 konu) yayınlanmış (v12) durumda. Mobil taraf **başlamadı**.
**Son güncelleme:** 26 Ağustos 2026

---

## 1. Karar Künyesi

| Konu | Karar | Gerekçe |
|---|---|---|
| Framework | Flutter (stabil kanal) | Tek kod tabanıyla iOS+Android; offline-first okuyucu için native performans şart değil |
| State management | Riverpod | Test edilebilirlik, DI'siz bağımlılık enjeksiyonu; sync client'ı ve local DB'yi provider olarak izole eder |
| Local veritabanı | **Drift** (SQLite üzerine tip-güvenli katman) | Veri gerçekten ilişkisel (Book→Module→Content→Block ağacı) — Hive/Isar gibi doküman-tabanlı çözümler bu şemayı zorlar. Backend'deki Postgres şemasıyla zihinsel model birebir örtüşür |
| Ağ katmanı | `dio` | İnterceptor desteği (checksum doğrulama, retry) `http` paketinden daha az boilerplate ile gelir |
| Navigasyon | `go_router` | Deep-link'e hazır, deklaratif; Modül → Konu → Detay üç seviyeli hiyerarşiyle uyumlu |
| Kimlik doğrulama | **Yok** | Sözleşme zaten anonim (§2, Sync-Sozlesmesi-v1.md) — mobil hiçbir zaman token taşımaz |
| Medya depolama | Uygulama belgeler dizininde, **checksum'a göre adlandırılmış dosya** (`{checksum}.{ext}`) | İçerik-adresli depolama: aynı medya iki content'te referans verilse bile bir kez iner, doğal dedup |
| Senkronizasyon tetikleyicisi | Uygulama açılışında otomatik `manifest` kontrolü + kullanıcının elle "güncellemeleri kontrol et" butonu | Arka plan senkronizasyonu (WorkManager/BGTaskScheduler) **v1 kapsamı dışı** — saha kullanımında pil/veri tüketimini kullanıcı kontrol etmeli |
| Video/Animasyon oynatıcı | `video_player` (native), Animasyon (`type=4`) için **v1'de placeholder** | Seed veride şu an video/animasyon örneği yok (Sync-Sozlesmesi §4); erken bağımlılık eklemeye gerek yok |
| Arama (full-text) | v1'de **yok**, modül/konu listesinde basit client-side filtre var | Backend'de full-text search altyapısı da yok (roadmap'te "Sonra" kararı) — ikisi birlikte gelecek |
| Test stratejisi | `flutter_test` (widget) + `mocktail` (sync client unit test) + `integration_test` (gerçek backend'e karşı tam senkronizasyon akışı) | Testing.md kuralı: unit + integration + E2E üçü de zorunlu |

### Neden bu üçü zaten kilitli (sözleşmeden miras)

Bunlar backend tarafında **donmuş** kararlar, mobil tarafın üzerine inşa ettiği zemin:
- Checksum doğrulama zorunlu (Sync-Sozlesmesi §5) — ham baytlar üzerinden SHA-256, büyük harf hex.
- `changes` bir günlüktür, tam kopya değildir — değişmeyen content hiç gelmez (§3.3).
- `modules` dizisi her `changes` yanıtında **toptan** gelir — diff yapılmaz, olduğu gibi yazılır (§7, §8).
- `variantGroupKey`/`variantLabel` dolu olan content'ler sekmeli tek ekranda birleştirilir, string parse ile değil (§3.2).

---

## 2. Mimari

### 2.1 Katman/klasör yapısı (feature-first)

```
lib/
├── core/
│   ├── sync/              SyncClient (manifest/snapshot/changes), checksum doğrulama
│   ├── database/          Drift şeması + DAO'lar
│   ├── media/              MediaDownloader (checksum-adresli dosya cache)
│   └── errors/             Sync.* hata kodlarının (§6) tipe çevrimi
├── features/
│   ├── book_list/          (v1'de tek kitap varsa bile ileride çoklu kitap için hazır)
│   ├── module_list/        Modül listesi ekranı
│   ├── content_list/       Bir modülün konuları (alt başlıklar)
│   ├── content_detail/     Blok render'ları (text/image/video/warning/table) + varyant sekmeleri
│   └── sync_status/        "Güncelleniyor", "Hazırlanıyor" (NotPublished), hata durumları
└── main.dart
```

### 2.2 Veri akışı

```
SyncClient ──HTTP──> Backend (/sync/manifest, /sync/snapshot, /sync/changes)
    │
    ▼ (checksum doğrulanmış JSON)
Drift (SQLite) ──sorgu──> Riverpod provider'lar ──> Widget ağacı
    ▲
    │
MediaDownloader (media.url → local dosya, checksum ile doğrula)
```

UI hiçbir zaman ağa doğrudan bakmaz — her zaman local Drift veritabanından okur. Bu, sahada
şebeke kesildiğinde uygulamanın davranışını değiştirmeden çalışmasını sağlar (offline-first'in
özü budur, sadece senkronizasyon aşaması ağ ister).

---

## 3. Local Veritabanı Şeması (Drift)

Backend şemasının salt-okunur bir aynası + senkronizasyon durumu için bir tablo:

| Tablo | Alanlar (özet) | Not |
|---|---|---|
| `sync_state` | `bookId`, `currentVersion`, `lastSyncedAt` | Tek satır per kitap; `changes` isteğinde `fromVersion` buradan okunur |
| `books` | `id`, `title`, `slug`, `description`, `languageCode`, `version` | |
| `modules` | `id`, `bookId`, `name`, `description`, `displayOrder` | Her `changes` yanıtında **tamamen** silinip yeniden yazılır (§7, Sync-Sozlesmesi) |
| `contents` | `id`, `moduleId`, `title`, `summary`, `displayOrder`, `variantGroupKey`, `variantLabel` | `deletedContentIds`'e göre silinir, `upsertedContents`'e göre upsert edilir |
| `content_blocks` | `id`, `contentId`, `type`, `text`, `dataJson`, `mediaId`, `displayOrder` | Content silinince cascade silinir |
| `media` | `id`, `url`, `checksum`, `size`, `localPath` (nullable — indirilene kadar null) | `localPath` doluysa dosya diskte var demektir |

**Kritik kural:** `contents`/`content_blocks` upsert'i **content bazında tam değiştirme**
olmalı (bloklarını sil + yeniden ekle), parça parça `UPDATE` değil — backend'in `PayloadJson`'ı
zaten tüm content'i atomik birim olarak taşıyor (bkz. CLAUDE.md "Canonical serialization").
Mobil tarafta da aynı atomiklik korunmalı; yoksa yarım güncellenmiş bir content ortaya çıkabilir.

---

## 4. Ekran Akışı (mockup'tan — 2026-08-25 tasarım incelemesi)

```
[Modül Listesi]                 [Konu Listesi]                [Konu Detayı]
title + subtitle kartları  ──>  modülün alt başlıkları   ──>  content block'ları sırayla
(BSAFE, Olay Yönetimi, ...)     (tıklanan modüle göre)         (text/image/table/warning)
                                                                 varyant grubu varsa sekmeli
```

- Modül kartı: `name` (title) + `description` (subtitle) — tasarımda "modül gibi" görünen, tıklanınca alt başlıklara açılan blok.
- Konu listesi: `content.title` + `content.summary`; `variantGroupKey` dolu olanlar **tek satırda**, grup içindeki `variantLabel`'lar sekme olarak.
- Konu detayı: `blocks[]` `displayOrder`'a göre sırayla render edilir, tip başına ayrı widget (bkz. §5).
- İlk açılış / henüz publish yoksa: `Sync.NotPublished` hatası → "İçerik hazırlanıyor" boş ekranı (backend hatası değil, meşru durum — Sync-Sozlesmesi §6).

---

## 5. Blok Tipi → Widget Eşlemesi

| `type` | Ad | Widget | Not |
|---|---|---|---|
| 1 | Text | `SelectableText` / `Text` (markdown değil, düz metin + `\n` satır sonları) | Backend `- ` ile başlayan madde listelerini düz metin olarak yazıyor; v1'de olduğu gibi gösterilir |
| 2 | Image | `Image.file(mediaLocalPath)` | İndirilmemişse yer tutucu + indirme göstergesi |
| 3 | Video | `video_player` + basit kontroller | v1'de seed veri yok, widget iskeleti hazır tutulur |
| 4 | Animation | Placeholder ikon + "desteklenmiyor" notu | Seed veri yok; gerçek ihtiyaç çıkınca tasarlanır (YAGNI) |
| 5 | Warning | Turuncu/kırmızı banner, `dataJson.severity`'ye göre renk | `dataJson` her zaman string — `jsonDecode` iki kez gerekebilir (Sync-Sozlesmesi §4 notu) |
| 6 | Table | `DataTable` (headers/rows `dataJson`'dan) | Geniş tablolarda yatay scroll (`SingleChildScrollView`) — mobil ekranda GCS/yoğunluk tabloları gibi çok sütunlu tablolar var |

---

## 6. Senkronizasyon İstemcisi — Davranış

Sync-Sozlesmesi-v1.md §7'deki akışın birebir uygulanması:

1. **İlk kurulum:** `manifest` → `snapshot` → checksum doğrula → Drift'e yaz (tüm tablolar,
   transaction içinde) → medyaları indir → `sync_state.currentVersion = manifest.version`.
2. **Güncelleme:** `manifest` → versiyon farklıysa `changes?fromVersion=local` → upsert/delete
   uygula → `modules` toptan yaz → medya diff'ini uygula → `sync_state.currentVersion = toVersion`.
3. **`Sync.InvalidFromVersion` alınırsa:** local sürüm bilgisini at, "İlk kurulum" akışına düş.
4. **Checksum tutmazsa:** snapshot'ı bir kez daha indir; ikinci hata da checksum tutmazsa
   kullanıcıya "indirme başarısız, tekrar deneyin" göster — sessizce bozuk veriyle devam **etme**.
5. Tüm DB yazımı **tek transaction** içinde olmalı — yarıda kesilen senkronizasyon (uygulama
   arka plana alınır, işlem sonlanır) önceki geçerli veriyi bozmamalı.

---

## 7. İş Kırılım Yapısı (WBS)

| # | Görev | Tahmini süre | Bağımlılık | Öncelik |
|---|---|---|---|---|
| 0.1 | Proje iskeleti: Flutter proje, lint kuralları, CI (build + `flutter test`) | 2,0 sa | — | KRİTİK |
| 0.2 | Riverpod + go_router + dio + Drift bağımlılıklarının kurulumu, boş ekran iskeletleri | 2,0 sa | 0.1 | KRİTİK |
| 1.1 | Drift şeması (§3) + migration | 2,5 sa | 0.2 | KRİTİK |
| 1.2 | `SyncClient`: `manifest`/`snapshot`/`changes` HTTP çağrıları + DTO'lar (Sync-Sozlesmesi §3) | 3,0 sa | 1.1 | KRİTİK |
| 1.3 | Checksum doğrulama (§5, ham bayt SHA-256) | 1,5 sa | 1.2 | KRİTİK |
| 1.4 | İlk kurulum akışı: snapshot → Drift yazımı (transaction) | 2,5 sa | 1.3 | KRİTİK |
| 1.5 | Güncelleme akışı: changes → upsert/delete/modül toptan yazım | 3,0 sa | 1.4 | KRİTİK |
| 1.6 | Hata sözleşmesi eşlemesi (`Sync.BookNotFound`/`NotPublished`/`InvalidFromVersion`, §6) | 1,5 sa | 1.5 | YAKIN |
| 2.1 | Modül listesi ekranı | 2,0 sa | 1.4 | KRİTİK |
| 2.2 | Konu listesi ekranı + varyant grubu sekmeleri (`variantGroupKey`/`variantLabel`) | 3,0 sa | 2.1 | KRİTİK |
| 2.3 | Konu detay ekranı: Text/Warning/Table render (§5) | 3,0 sa | 2.2 | KRİTİK |
| 2.4 | Image block render + `MediaDownloader` (checksum-adresli local cache) | 3,0 sa | 2.3 | KRİTİK |
| 2.5 | Video/Animation placeholder widget'ları | 1,0 sa | 2.4 | SONRA |
| 3.1 | "Hazırlanıyor" / hata / boş durum ekranları | 2,0 sa | 1.6 | YAKIN |
| 3.2 | Elle "güncellemeleri kontrol et" + senkronizasyon ilerleme göstergesi | 2,0 sa | 1.5 | YAKIN |
| 3.3 | Client-side arama/filtre (modül + konu başlıklarında) | 2,0 sa | 2.2 | SONRA |
| 4.1 | Widget testleri (her blok tipi için) | 3,0 sa | 2.4 | YAKIN |
| 4.2 | `SyncClient` unit testleri (mocktail ile HTTP mock) | 2,5 sa | 1.6 | YAKIN |
| 4.3 | Integration test: gerçek backend'e karşı tam ilk-kurulum + güncelleme akışı | 3,0 sa | 4.2 | YAKIN |
| 5.1 | Türkçe karakter/font doğrulaması, erişilebilirlik (kontrast, yazı boyutu) taraması | 1,5 sa | 2.3 | YAKIN |
| 5.2 | Release hazırlığı: ikon, splash, sürüm numarası, store metadata | 2,0 sa | 4.3 | SONRA |

**Toplam (KRİTİK + YAKIN):** ~35 saat · (+ SONRA havuzu ~6 saat, kesilebilir)

---

## 8. Milestone'lar

| Kod | Ad | Kapsam | Kanıt |
|---|---|---|---|
| **MM1** | Sync İstemcisi Çalışıyor | 1.1-1.6 | Konsoldan/test'ten `snapshot` çekilip Drift'e yazılıyor, checksum doğrulanıyor |
| **MM2** | Okuyucu MVP | 2.1-2.4 | Gerçek kitap içeriği telefonda modül→konu→detay olarak geziliyor, görseller yükleniyor |
| **MM3** | Offline Dayanıklılık | 3.1-3.3 | Uçak modunda uygulama sorunsuz açılıyor; güncelleme akışı gerçek `changes` ile test edildi |
| **MM4** | Test Kapsamı | 4.1-4.3 | CI'da widget + unit + integration testleri yeşil |
| **MM5** | Yayına Hazır | 5.1-5.2 | Store'a yüklenebilir build, Türkçe içerik tüm ekranlarda doğru render |

---

## 9. Risk Kayıt Defteri

| # | Risk | Etki | Olasılık | Azaltma |
|---|---|---|---|---|
| 1 | Yarıda kesilen senkronizasyon eski veriyi bozar | 🔴 Kritik | Orta | Tüm Drift yazımı tek transaction (§6 madde 5) |
| 2 | Checksum uyuşmazlığı sessizce yutulur, bozuk içerik gösterilir | 🔴 Kritik | Düşük-Orta | §6 madde 4 — checksum tutmadan asla local DB'ye yazma |
| 3 | Büyük medya indirmeleri sahada zayıf bağlantıda zaman aşımına uğrar | 🟠 Yüksek | Yüksek | `dio` retry/backoff interceptor, indirme durumunu UI'da göster, kısmi indirmeye devam edebilme |
| 4 | `variantGroupKey` gruplaması yanlış yorumlanır (string parse'a kayar) | 🟠 Orta | Düşük | Sync-Sozlesmesi §3.2 net: sadece bu iki alanla grupla, `title`'a bakma — kod review'da kontrol maddesi |
| 5 | Sözleşmeye v1.2+ additive alan eklenir, mobil bilmediği alanı kırılma sanır | 🟡 Orta | Orta | JSON parse'ta bilinmeyen alanı sessizce yok say (Sync-Sozlesmesi §9) — DTO'larda `unknown fields ignored` ayarı baştan aç |
| 6 | Tek geliştirici / kesinti riski (backend tarafındaki gibi) | 🟠 Yüksek | Yüksek | Her milestone bağımsız durulabilir; MM1-MM2 tek başına demo edilebilir MVP |

---

## 10. Açık Kararlar (kullanıcıdan girdi gerekiyor)

Bu roadmap'i ilerletmeden önce netleşmesi gereken, backend'in belirleyemeyeceği ürün kararları:

| Soru | Neden önemli |
|---|---|
| Minimum Android/iOS sürüm hedefi? | Drift/video_player paket uyumluluğunu ve test matrisini belirler |
| Uygulama mağazadan mı dağıtılacak, yoksa APK/TestFlight ile ekip içi mi? | Store review süreci, imzalama, CI/CD hattı farklılaşır |
| Karanlık mod gerekli mi? | `web/design-quality.md` kuralı: "karanlık moda otomatik geçme, kasıtlı seç" — ürün kararı |
| Kaza-kırım/analitik toplama (Crashlytics vb.) izinli mi? | KVKK/gizlilik değerlendirmesi gerektirir, anonim-kullanıcı ilkesine aykırı olabilir |
| Çoklu kitap senaryosu ne zaman gerçek olur? | Şema zaten destekliyor (§1 not) ama UI'da "kitap seçici" ekranı MM2 kapsamında mı, ertelenir mi? |

---

## 11. Backend Tarafında Beklemede Olanlar

Mobil geliştirme başladığında backend'den ayrıca istenebilecekler (henüz talep gelmedi, olası):
- Medya için CDN/imzalı URL (şu an local disk + `IStorageService` — roadmap §1'de "MinIO'ya geçiş tek sınıf" notu var, ihtiyaç netleşirse).
- Parça-başına checksum (`changes` içinde) — Sync-Sozlesmesi §9'da bilerek v1.0 dışı bırakıldı, ihtiyaç kanıtlanırsa v1.1+ additive alan.
- Public web okuyucu API'si — Faz 4 tasarım notlarında bahsi geçiyor (roadmap satır 825+), mobil ile aynı yayın tablolarından beslenecek ama ayrı endpoint seti.
