# Frontend Notları ve Gelecek Özellik Önerileri

Bu dosya, backend entegrasyonu sırasında fark edilen ama sözleşme kapsamında olmadığı için
şu an uygulanmayan konuları not almak için tutuluyor. Aksiyon alınması gerekmiyor, sadece
ileride bir özellik/karar gerektiğinde referans olsun diye.

---

## 1. ~~Acil Durum Bandı (Emergency Banner)~~ — KALDIRILDI (2026-08-30)

Bu özellik `Dashboard.jsx`, `UserLayout.jsx` ve `src/components/ui/EmergencyBanner.jsx`'ten
tamamen kaldırıldı. Sebep: sadece tarayıcı `localStorage`'ında tutuluyordu, backend'de hiç
karşılığı yoktu — admin bandı "yayınladığında" bu sadece kendi tarayıcısında görünüyordu,
başka ziyaretçiler görmüyordu. Karar: özellik backend'de gerçek bir karşılığı olmadan
frontend'de tutulmayacak. İleride gerçek bir duyuru/banner özelliği istenirse, backend'de
(Book veya global ayar üzerinde `active`/`message` alanları + okuyucu tarafının görebileceği
anonim bir endpoint) baştan tasarlanmalı.

---

## 2. ~~Modül/İçerik `isPublished` flag'i publish davranışını etkilemiyor~~ — ÇÖZÜLDÜ (13.3)

`SnapshotBuilder.BuildSnapshot` artık Module/Content'i kendi `IsPublished` bayrağına göre
süzüyor — beklenen davranış ("Taslak" işaretli içerik publish'te hariç tutulur) artık gerçek.
Filtre eklenmeden önce canlı kitaptaki 8/10 Modül ve 85/97 Content'in `IsPublished=false`
olduğu (zaten servis ediliyor olmasına rağmen) tespit edilip kapsamlı bir SQL backfill'le
düzeltildi — filtre önce eklenseydi bu içerikler sessizce tombstone'lanırdı.

---

## 3. ~~CORS izin listesi çok dar, sessizce hata veriyor~~ — ÇÖZÜLDÜ

`Cors:AllowedOrigins` zaten config-driven'dı (env değişkeninden okunuyor, `CORS_ALLOWED_ORIGIN_0..`
ile de override edilebiliyor — 13.1'de netleştirildi). Dev listesine `5174`/`4173` de eklendi
(`appsettings.Development.json`). **Prod domain'i allowlist'e eklemeyi unutmamak hâlâ deploy
öncesi kritik bir adım** — bu backend tarafında otomatikleşmiyor, deploy checklist'inde kalmalı.

---

## 4. ~~Login başarısız olduğunda 401'in gövdesi dolu — ama sözleşme dokümanı bunu netleştirmiyor~~ — ÇÖZÜLDÜ (13.1)

`CMS-API-Sozlesmesi-v1.md` §3.4'e istisna notu eklendi: "`/auth/login` hariç her uçtaki 401 boş
gövdelidir" — login'in kendi 401'inin (yanlış kullanıcı adı/şifre) dolu bir ProblemDetails
döndüğü artık açıkça yazılı, başka bir istemci (mobil vs.) aynı yanlış varsayıma düşmez.

---

## 5. ~~İçerik listesi book/modül seviyesinde toplu çekilemiyor (N+1 sorunu)~~ — ÇÖZÜLDÜ (2026-08-31)

Dashboard'daki içerik sayısı istatistiği daha önce (13.4) `GET /books/{bookId}/modules`
yanıtına eklenen `contentCount` alanıyla çözülmüştü. Kalan parça — admin panelin "İçerikler"
ekranının tüm modüllerdeki tüm içerikleri tek listede göstermesi — artık `GET
/books/{bookId}/contents` ile çözüldü: aynı `ContentDto` şeklini (aynı sayfalama zarfı,
`isPublished` filtresi dahil) tek çağrıda, modül sırası → modül içi `displayOrder` ile döner.
Eski N+1 akışı (`GET /books/{id}/modules` + her modül için ayrı `GET
/modules/{moduleId}/contents`) artık gerekli değil.

---

## 6. ~~Blok "reorder" işlemi, sırası değişmeyen blokların `dataJson`'ını da yeniden biçimlendiriyor~~ — ÇÖZÜLDÜ (13.5)

`ReorderHelper`'ın `markDirty`'si artık tüm entity'yi değil sadece `DisplayOrder` kolonunu kirli
işaretliyor (`IRepository<T>.UpdateProperty`) — reorder artık `ContentBlock.DataJson`'a hiç
dokunmuyor, `updatedAt` de gereksiz bump'lanmıyor. Düzeltme sırasında bir yan bulgu: Postgres
jsonb kolonu zaten İLK INSERT'te kendi kanonik biçimine dönüştürüyor (anahtar sırası uzunluğa
göre değişiyor) — yani `dataJson`'ın gönderilen string'le birebir aynı kalacağı garantisi hiç
olmamıştı, sadece reorder'ın onu GEREKSİZ YERE tekrar yeniden yazması önlendi.

---

## 7. ~~`/users` sadece oluşturma destekliyor~~ — ÇÖZÜLDÜ + frontend'e bağlandı (2026-08-30)

Backend `GET /users`, `PUT /users/{id}/role`, `POST /users/{id}/deactivate`,
`PUT /users/me/password` endpoint'lerini ekledi; gerçek backend'e karşı canlı test edilip
(test kullanıcıları oluşturulup, rol değiştirilip, pasifleştirilip, sonra pasifleştirilerek
temizlendi) `Settings.jsx`'e bağlandı:

- **Kullanıcılar listesi** (Admin) — `UserDto`: `{id (guid), userName, fullName, roles[], isLockedOut}`.
  Kendi hesabınız için rol değiştirme/pasifleştirme butonları devre dışı bırakılıyor (yanlışlıkla
  kendi kendini kilitlemeyi önlemek için).
- **Rol değiştir** (Admin) — `PUT /users/{id}/role`, 200 + güncel `UserDto` döner.
- **Pasifleştir** (Admin) — `POST /users/{id}/deactivate`, 204. **Doğrulandı: geri alan bir
  endpoint yok** (`activate` yok) — pasifleştirilen kullanıcı bir daha giriş yapamıyor
  (`Auth.InvalidCredentials` ile aynı jenerik 401'i alıyor, hesabın kilitli olduğu ayrıca
  belirtilmiyor — güvenlik açısından doğru davranış). Frontend'de bu geri alınamazlığı
  onay diyaloğunda açıkça belirtiyoruz.
- **Kendi şifreni değiştir** (herkes, Editor dahil) — `PUT /users/me/password`, 204. Yanlış
  `currentPassword` → 400 `User.PasswordChangeFailed`. ~~`detail` İngilizce geliyor~~ —
  **ÇÖZÜLDÜ**: backend'e `TurkishIdentityErrorDescriber` eklendi, `UserManager`'dan gelen tüm
  `IdentityResult` mesajları (şifre politikası, "already in role" vb. dahil, sadece bu tek örnek
  değil) artık Türkçe. `detail` artık "Mevcut şifre hatalı." döner.

**Rol kısıtlamaları canlı test edildi:** `GET/POST /users`, `PUT .../role`, `POST .../deactivate`,
`POST /media/cleanup-orphans`, `POST /books/{id}/rollback` → Editor token'ıyla hepsi **403**.
`PUT /users/me/password` → Editor'e de **izinli** (kendi şifresi, beklenen).

**Rollback** (`POST /books/{bookId}/rollback`, body `{"toVersion": int}`) `Dashboard.jsx`'e
eklendi, Admin-only. **Önemli kısıt:** backend'de geçmiş sürümleri listeleyen bir endpoint
yok, admin hangi sürüme döneceğini elle bilmek zorunda (sadece `book.version` — o anki
sürüm — biliniyor). Gerçek bir rollback'i paylaşılan kitaba karşı test etmedim (canlı veriyi
bozma riski), ama geçersiz/olmayan `toVersion` ile hata şeklini doğruladım:
`404 Publishing.VersionNotFound`.

**Medya temizliği** (`POST /media/cleanup-orphans`) `Settings.jsx`'e "Bakım" kartı olarak
eklendi, Admin-only. Canlı test edildi — yanıt düz bir sayı (`0` gibi, silinen dosya sayısı),
obje değil.

---

## 8. Diğer küçük notlar

- `src/data/mock.js` ve `src/pages/user/InfoPage.jsx` kod tabanında duruyor ama hiçbir yerden
  kullanılmıyor (InfoPage `App.jsx`'te route'lanmamış). Çalışan uygulamayı etkilemiyorlar,
  istenirse temizlenebilir.
- Admin panelinde kitap seçici yok; tek kitap olduğu varsayımıyla `VITE_DEFAULT_BOOK_ID` env
  değişkenine sabitlendi (bkz. `README.md`). Backend'de ikinci bir kitap açılırsa bu varsayım
  kırılır, bir kitap seçici eklemek gerekir.

---

## 9. Yeni kullanıcı yönetimi endpoint'lerinden çıkan iki soru (2026-08-30)

Kullanıcı yönetimini frontend'e bağlarken canlı testte karşıma çıkan, backend'e sorulmaya
değer iki nokta:

**a) ~~Pasifleştirilen bir kullanıcıyı geri aktive etmenin yolu yok~~ — ÇÖZÜLDÜ (2026-08-30).**
`POST /users/{id}/activate` eklendi (Admin-only, gövde yok, `LockoutEnd`'i kaldırır,
idempotent). Kasıtlı bir "kalıcı pasifleştirme" tasarımı değilmiş — eksik kalmış bir
uç noktaymış, tamamlandı. Frontend'deki "geri alınamaz" uyarısı artık kaldırılabilir/
yumuşatılabilir; kullanıcı `activate` ile geri getirilebiliyor (refresh token'ları geri
gelmiyor, kullanıcı yeniden login olmak zorunda — beklenen).

**b) ~~Rollback için hangi sürümlerin var olduğunu görecek bir endpoint yok~~ — ÇÖZÜLDÜ (2026-08-30).**
`GET /books/{bookId}/publications` eklendi (Admin-only) — kitabın tüm yayın geçmişini
(en yeniden eskiye) `{publicationId, version, publishedAt, publishedByUserName, contentCount,
checksum}` şeklinde döner, `SnapshotJson` hiç taşımaz (küçük payload). Kitap hiç
yayınlanmamışsa boş dizi (hata değil). Rollback UI'ı artık gerçek bir "sürüm geçmişi"
dropdown'ına çevrilebilir — `toVersion`'ı elle yazmaya gerek kalmadı.

---

## 10. "Oturum süresi doldu" bazen sebepsiz çıkıyor, elle tekrar login gerekiyor (2026-08-31)

Kullanıcı geri bildirimi: bazen (deterministik değil, ara sıra) uygulama oturumun bittiğini
söylüyor ve elle çıkış yapıp tekrar login olmak gerekiyor — normal şartlarda access token
süresi dolduğunda (`Jwt:ExpiryMinutes=60`) frontend'in sessizce `POST /auth/refresh` çağırıp
kullanıcıyı hiç rahatsız etmemesi beklenir.

**Backend tarafında araştırılan muhtemel kök neden — bkz. roadmap doc 13.10 için tam teknik
detay:** `AuthService.RefreshAsync` refresh token'ı **tek kullanımlık rotasyon** ile çalıştırıyor
ve zaten iptal edilmiş bir token tekrar sunulursa bunu "çalınmış olabilir" sayıp kullanıcının
TÜM token'larını (yeni alınanlar dahil) iptal ediyor — bu, **eşzamanlı iki refresh isteği**
(iki sekme, ya da access token süresi dolduğunda birden fazla API çağrısının aynı anda refresh
tetiklemesi) durumunda YANLIŞLIKLA tetiklenip kullanıcıyı hatasızken zorla login'e düşürebilir.
"Bazen" olması (zamanlamaya bağlı bir yarış durumu) bu teoriyle örtüşüyor — henüz doğrulanmadı.

**Frontend tarafında kontrol edilmesi gereken iki şey:**
- Web dashboard'ın 401 yakalayıp otomatik `refresh` + orijinal isteği tekrar deneme mantığı
  (interceptor) var mı, yoksa her 401'de direkt kullanıcıya mı gösteriliyor? Yoksa bu daha basit
  ve daha olası açıklama — backend'in refresh mekanizması hiç devreye girmiyor demektir.
- Varsa, bu interceptor **tek-uçuşlu (single-flight)** mi — yani aynı anda birden fazla istek
  401 alırsa hepsi TEK bir refresh çağrısını mı bekliyor, yoksa her biri kendi refresh'ini mi
  tetikliyor? İkincisi yukarıdaki race'i doğrudan tetikler. Çözüm: ilk 401 refresh'i başlatır,
  diğerleri onun sonucunu bekler, ayrı ayrı refresh çağırmaz.
- Sadece refresh de başarısız olursa (gerçek oturum sonu) kullanıcıyı **otomatik** `/login`'e
  yönlendirmek — "oturum süresi doldu" diye bir mesaj gösterip elle çıkışı beklemek yerine.

Backend tarafında bir düzeltme (rotasyona kısa bir grace window eklemek) mümkün ama kök neden
doğrulanmadan (loglarla ya da tekrar üretilerek) yapılırsa yarım çözüm olur — önce yukarıdaki
frontend kontrolü yapılmalı.
