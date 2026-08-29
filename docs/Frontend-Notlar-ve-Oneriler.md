# Frontend Notları ve Gelecek Özellik Önerileri

Bu dosya, backend entegrasyonu sırasında fark edilen ama sözleşme kapsamında olmadığı için
şu an uygulanmayan konuları not almak için tutuluyor. Aksiyon alınması gerekmiyor, sadece
ileride bir özellik/karar gerektiğinde referans olsun diye.

---

## 1. Acil Durum Bandı (Emergency Banner) — şu an sadece localStorage'da, DB'de değil

`src/components/ui/EmergencyBanner.jsx` ve `Dashboard.jsx`'deki "Yayına Al" kontrolü, backend
sözleşmesinde (`CMS-API-Sozlesmesi-v1.md`) hiç karşılığı olmayan bir özellik. Şu anki hali:
admin panelde bandı açtığında bu sadece **o admini'nin kendi tarayıcısının localStorage'ına**
yazılıyor; başka bir cihazdan/tarayıcıdan siteye giren ziyaretçi bunu görmüyor. Yani gerçek bir
yayın mekanizması değil, tek-tarayıcılık bir demo.

**Gerçek hale getirmek için backend'de gerekenler:**
- Book (veya global bir ayar) üzerinde `emergencyBannerActive: bool` ve `emergencyBannerMessage: string`
  gibi alanlar; bunları okuyucu tarafının da görebileceği bir yol (Sync manifest'e eklenebilir,
  ya da ayrı bir `GET /api/v1/announcements` gibi anonim bir endpoint).
- CMS tarafında bunu güncelleyecek bir `PUT` endpoint'i (muhtemelen Admin rolü ile sınırlı).

**Alternatif:** Backend'de bu öncelikli değilse, admin panelindeki bu kontrolü kaldırmak daha
doğru olur — şu anki hali "bunu yayınladım, herkes görüyor" izlenimi veriyor ama vermiyor.

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

## 5. İçerik listesi book/modül seviyesinde toplu çekilemiyor (N+1 sorunu)

Admin panelindeki "İçerikler" listesi (tüm modüllerdeki tüm içerikleri tek ekranda göstermek)
ve Dashboard'daki içerik sayısı istatistiği, backend'de kitap/modül genelinde tek bir "tüm
içerikleri getir" ya da "modül başına içerik sayısı" endpoint'i olmadığı için N+1 sorgu ile
çalışıyor: önce `GET /books/{id}/modules`, sonra her modül için ayrı ayrı
`GET /modules/{moduleId}/contents`. Şu an 10 modülle sorun değil ama modül sayısı arttıkça
admin panel yavaşlayacak.

**Önerilir (öncelik değil, backend'in planına bağlı):**
- `GET /books/{bookId}/contents` gibi düz (flat), opsiyonel modül filtresi olan bir liste
  endpoint'i, veya
- `GET /books/{bookId}/modules` yanıtına her modül için `contentCount` alanı eklemek — bu tek
  başına Dashboard'daki N+1'i tamamen ortadan kaldırır.

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

## 7. `/users` sadece oluşturma destekliyor — listeleme/güncelleme/silme yok

Admin panelinde "Ayarlar" sayfasına yeni Editor/Admin ekleme özelliğini `POST /users` üzerinden
bağladık. Ama mevcut kullanıcıları **listeleyecek**, rolünü **değiştirecek**, hesabı
**pasifleştirecek/silecek** veya kendi şifresini **değiştirecek** bir endpoint yok. Şu an bu
işlemler için admin panelde hiçbir arayüz sunmuyoruz (yoktular zaten, ama gerçek bir kullanıcı
yönetimi ekranı için bu dörtlü gerekecek).

---

## 8. Diğer küçük notlar

- `src/data/mock.js` ve `src/pages/user/InfoPage.jsx` kod tabanında duruyor ama hiçbir yerden
  kullanılmıyor (InfoPage `App.jsx`'te route'lanmamış). Çalışan uygulamayı etkilemiyorlar,
  istenirse temizlenebilir.
- Admin panelinde kitap seçici yok; tek kitap olduğu varsayımıyla `VITE_DEFAULT_BOOK_ID` env
  değişkenine sabitlendi (bkz. `README.md`). Backend'de ikinci bir kitap açılırsa bu varsayım
  kırılır, bir kitap seçici eklemek gerekir.
