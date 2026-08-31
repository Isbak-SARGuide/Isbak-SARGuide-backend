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

## 2. Modül/İçerik `isPublished` flag'i publish davranışını etkilemiyor

Test edildi (2026-08-28, gerçek backend'e karşı): `BSAFE` modülü draft tarafında
`isPublished: false` iken, kitabın zaten yayınlanmış v17 sürümünde de mevcut. Yani
`POST /books/{id}/publish` çağrıldığında modül/içerik seviyesindeki `isPublished` durumları
**hiç süzülmüyor** — o anki draft ağacının tamamı (flag'ler ne olursa olsun) donup yeni
sürüm oluyor.

Bu muhtemelen kafa karıştırıcı: admin panelinde bir içeriği "Taslak" işaretlemek, kullanıcının
"bu henüz yayınlanmasın" diye düşünmesine yol açabilir, ama pratikte publish anında yine gidiyor.

**Olası çözümler (backend kararı gerekir):**
- Publish sırasında gerçekten `isPublished: false` olan modül/içerikleri hariç tutmak (asıl
  beklenen davranış muhtemelen bu), veya
- Frontend'de bu flag'in adını/açıklamasını değiştirip ("yayında/taslak" yerine "gözden
  geçirildi" gibi) yanıltıcı olmaktan çıkarmak.

Şu an frontend hiçbir varsayımda bulunmuyor, flag'i olduğu gibi gösterip gönderiyor — davranış
backend'in gerçek mantığına bağlı.

---

## 3. CORS izin listesi çok dar, sessizce hata veriyor

Backend şu an sadece `http://localhost:5173` ve `http://localhost:3000` origin'lerine
`Access-Control-Allow-Origin` dönüyor (test edildi, 2026-08-28). Bunun dışındaki hiçbir origin
(örn. Vite başka bir portta açılırsa — `5174`, `5175`... — veya `vite preview`'in kullandığı
`4173`, ya da ileride production domain) **hiç CORS header'ı almıyor**, tarayıcı isteği sessizce
engelliyor. Bu, geliştiriciye "neden her şey hata veriyor" diye saatler kaybettirebilecek türden
bir hata — tarayıcı konsolunda CORS hatası görünür ama network sekmesinde istek "başarılı" (200)
görünebildiği için ilk bakışta backend'in çalıştığı, frontend'in bozuk olduğu sanılabiliyor.

**Önerilir:**
- Prod domain'i CORS allowlist'ine eklemeyi unutmamak (deploy öncesi kritik).
- Dev ortamı için allowlist'i env değişkeninden okunan, virgülle ayrılmış bir liste yapmak
  (`Cors:AllowedOrigins`), böylece her yeni port için kod değişikliği gerekmez.
- İzin verilmeyen bir origin'den istek geldiğinde CORS'un kendisi zaten sessiz kalıyor
  (tarayıcı standardı), bu değiştirilemez — ama en azından dokümantasyonda "izinli origin
  listesi X'tir, değişmesi gerekiyorsa backend ekibine söyleyin" notu bırakmak faydalı olur.

---

## 4. Login başarısız olduğunda 401'in gövdesi dolu — ama sözleşme dokümanı bunu netleştirmiyor

`CMS-API-Sozlesmesi-v1.md` §3.4, 401'i "gövdesi boş, ProblemDetails değil" diye tanımlıyor —
ama bu tanım sadece **token yok/geçersiz** durumuna (JWT middleware'in kendi challenge'ı) ait.
Test ettim: `POST /auth/login`'e yanlış şifre/kullanıcı adıyla gidildiğinde dönen 401'in gövdesi
**dolu bir ProblemDetails** (`title: "Auth.InvalidCredentials"`, `detail: "Kullanıcı adı veya
şifre hatalı."`) — yani domain seviyesinde bir 401. Sözleşme dokümanına "login'deki 401 farklıdır,
gövdesi doludur" diye bir netleştirme eklenmesi iyi olur; frontend tarafında bunu ayırt eden bir
düzeltme zaten yaptık (önce boş gövde mi diye bakıp öyle karar veriyoruz) ama dokümanda bu ayrım
yoktu, başka bir istemci (mobil, vs.) aynı yanlışa düşebilir.

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

## 6. Blok "reorder" işlemi, sırası değişmeyen blokların `dataJson`'ını da yeniden biçimlendiriyor

Test ettim: bir Table bloğu oluşturulduğunda `dataJson` `{"headers":[...],"rows":[...]}` şeklinde
(boşluksuz, sıralı) kaydediliyor. Sonra o bloğu **içeriğine hiç dokunmadan**, sadece
`PUT /contents/{id}/blocks/reorder` ile başka bir bloğun önüne/arkasına taşıdığımda, geri
gelen `dataJson` `{"rows": [...], "headers": [...]}` haline dönüyor — anahtar sırası değişmiş,
boşluklar eklenmiş, `updatedAt` de bump'lanmış. Yani reorder endpoint'i muhtemelen etkilenen
blokları deserialize edip tekrar serialize ederek kaydediyor, sadece `displayOrder` kolonunu
güncellemek yerine.

Fonksiyonel bir bozukluk değil (JSON hâlâ geçerli, aynı veri), ama iki açıdan dikkat çekici:
- `updatedAt`'in içerik değişmediği halde değişmesi, ileride "son değişiklik ne zaman oldu"
  gibi bir mantık kurulursa yanıltıcı olabilir.
- Sync tarafının checksum/entegrity hikâyesi (`Sync-Sozlesmesi-v2.md` §5) "ham baytlar" üzerinden
  çalışıyor; bir reorder'ın alakasız bir bloğun byte'larını değiştirmesi, `/sync/changes`
  delta'sının gereğinden büyük/gereksiz diff üretmesine yol açabilir (içerik aynı olsa bile
  formatı değiştiği için "değişti" sayılabilir). Şimdilik bir sorun yaratmıyor ama backend
  ekibinin bilmesi iyi olur.

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
