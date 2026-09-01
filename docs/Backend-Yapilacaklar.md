# Backend'den İstenenler — Web Entegrasyonu Bulguları

**Son güncelleme:** 2026-09-01
**Kaynak:** `Frontend-Notlar-ve-Oneriler.md`'deki detaylı günlüğün, hâlâ açık olan maddeleri
süzülmüş hali. Çözülmüş maddeler burada yok — sadece backend tarafında hâlâ bir aksiyon
bekleyenler.

---

## 1. CORS allowlist production'a hazır değil (ÖNCELİKLİ)

Şu an sadece `http://localhost:5173` ve `http://localhost:3000` origin'lerine
`Access-Control-Allow-Origin` dönüyor. Bunun dışındaki her origin (production domain dahil)
**hiç CORS header'ı almıyor**, tarayıcı isteği sessizce engelliyor — network sekmesinde 200
görünse bile JS tarafı cevabı okuyamıyor.

**İstenen:**
- Deploy öncesi production domain'i allowlist'e eklemek
- İzin verilen origin listesini env değişkeninden okunan, virgülle ayrılmış bir konfigürasyona
  taşımak (`Cors:AllowedOrigins`), her yeni ortam için kod değişikliği gerekmesin

---

## 2. `pageSize`'a sunucu tarafında üst sınır yok (GÜVENLİK)

`GET /books/{id}/contents` (muhtemelen tüm `PagedResult<T>` uçları) `pageSize=100000` gibi
büyük pozitif değerleri aynen kabul ediyor. Negatif/0 doğru şekilde varsayılana (`50`) düşüyor,
sadece büyük pozitif değerlerde tavan yok.

**İstenen:** `pageSize` için bir üst sınır (örn. 200–500) — aşan istekler ya kırpılmalı ya da
`400 Validation` ile reddedilmeli. Şu an veri küçük olduğu için zararsız ama kötü niyetli/buggy
bir istemci sunucuyu gereksiz büyük bir sorguya zorlayabilir.

---

## 3. Modül/İçerik `isPublished` flag'i publish mekanizmasıyla tamamen bağlantısız (BACKEND — netleşmeli/muhtemelen bug)

**2026-09-01'de temiz, kontrollü bir testle kesinleştirildi** (frontend sorunu değil, root
cause backend'de): `isPublished: false` bir test modülü + içeriği oluşturup gerçekten
yayınladım (`POST /books/1/publish`, v22→23→24), sonra aynı kayıtları tekrar çektim.

Sonuç: **hem modülde hem içerikte `isPublished` publish öncesi/sonrası birebir `false` kaldı,
`updatedAt` bile değişmedi** — yani publish handler'ı bu satırlara hiç dokunmuyor. Bu, daha önce
`BSAFE` modülüyle gözlemlenen "taslak işaretli içerik yine de yayınlanan sürüme dahil oluyor"
bulgusuyla birleşince şunu kanıtlıyor:

- Publish sırasında `isPublished` **süzülmüyor** (taslak olsa da içerik yine gidiyor)
- Publish sonrası `isPublished` **otomatik `true` olmuyor** (elle set edilmediği sürece hep
  `false` kalıyor)

Yani bu alan şu an **admin panelinde gösterilen/düzenlenebilen ama gerçek yayın mekanizmasıyla
sıfır bağlantısı olan** bir alan — kullanıcıyı doğrudan yanıltıyor (bir içeriği "Taslak"
işaretlemek, "bu yayınlanmayacak" izlenimi veriyor ama publish'te yine gidiyor; publish
edildikten sonra da "Yayında" görünmesi gerekirken hâlâ "Taslak" görünüyor).

**Karar gerekiyor — muhtemelen ikisi birden yapılmalı:**
1. Publish sırasında gerçekten `isPublished: false` olanlar hariç tutulmalı (asıl beklenen
   davranış büyük ihtimalle bu), VE
2. Publish başarılı olduğunda, o publish'e dahil edilen modül/içeriklerin `isPublished`'i
   `true`'ya çevrilmeli — böylece flag gerçekten "şu an yayında mı" sorusuna cevap versin.

Bu ikisi yapılmazsa, frontend'deki "Taslak/Yayında" etiketinin anlamı yok — sadece backend'in
hiç okumadığı bir metadata alanı olarak kalıyor. Hangisi seçilirse seçilsin, admin paneldeki
metni ona göre netleştireceğiz.

---

## 4. Küçük tutarlılık düzeltmeleri

- **`PUT /users/me/password`** yanlış `currentPassword` hatasında `detail` alanı İngilizce
  geliyor (`"Incorrect password."`) — sistemdeki diğer tüm hata mesajları Türkçe. Tutarlılık
  için Türkçeleştirilmesi iyi olur.
- **`CMS-API-Sozlesmesi-v1.md` §3.4** — 401'in "gövdesi boş" tanımı sadece token-yok/geçersiz
  durumuna ait; `POST /auth/login`'deki domain-seviyeli 401 (`Auth.InvalidCredentials`) dolu bir
  `ProblemDetails` gövdesi taşıyor. Dokümana bu ayrımın eklenmesi, ileride başka bir istemcinin
  (mobil gibi) aynı varsayım hatasına düşmesini önler.
- **Blok "reorder" endpoint'i** (`PUT /contents/{id}/blocks/reorder`), sırası değişmeyen
  blokların `dataJson`'ını da deserialize edip yeniden serialize ediyor gibi görünüyor (anahtar
  sırası/boşluklar değişiyor, `updatedAt` bump'lanıyor) — fonksiyonel bir sorun değil ama
  sadece `displayOrder` güncellemesi beklenirken içerik baytları da değişiyor. Sync tarafının
  "ham bayt" checksum modeliyle ileride gereksiz diff'e yol açabilir, bilginize.

---

## 5. Aynı içerikli medya tekrar yüklenince kalıcı `500` hatası (BUG — üretilebilir/tekrarlanabilir)

**2026-09-01'de kanıtlandı ve izole edildi.** Aynı bayt içeriğine sahip bir görsel iki kez
yüklendiğinde (örn. aynı ikon/logoyu iki farklı blokta kullanmak için tekrar yüklemek, ya da
kullanıcı yükle butonuna yanlışlıkla iki kez basmak), ikinci yükleme her seferinde şunu
döndürüyor:

```
HTTP 500
{"title":"Media.ConcurrentUploadUnresolved",
 "detail":"Eşzamanlı yükleme çözümlenemedi, lütfen tekrar deneyin."}
```

**Test ettim, "tekrar deneyin" mesajına rağmen kendi kendine düzelmiyor** — aynı içerikle 3+
kez, aralarda birkaç saniye bekleyerek tekrar denedim, hepsi aynı hatayı verdi. Farklı (benzersiz)
içerikli bir dosya hemen ve sorunsuz yüklendi — yani sorun genel yükleme akışında değil,
**belirli bir içerik hash'ine özel, kalıcı bir kilit/durum** gibi görünüyor.

**Şüphelendiğim kök neden:** İçerik-adresli depolama (storagePath dosya adı, yüklenen içeriğin
hash'inden üretiliyor — `media/2026/09/<hash>.webp`) muhtemelen aynı hash için bir
"işleniyor/kilitli" kaydı tutuyor. Bu testte önce aynı içerikle bir medya yükleyip **soft-delete
ile sildim**, sonra aynı içeriği tekrar yüklemeyi denedim — o andan itibaren o içerik hash'i için
her yükleme kalıcı olarak bu hatayı vermeye başladı. Yani soft-delete, DB satırını siliyor ama
altındaki dedup/kilit kaydını (varsa) temizlemiyor olabilir.

**Gerçek etki:** Bir editör aynı görseli (örn. bir logo, tekrarlayan bir uyarı ikonu) birden
fazla içerikte kullanmak isterse, ya da yükleme butonuna çift tıklarsa, o dosya için **bir daha
hiç** yükleme yapamaz hale geliyor (kalıcı, retry ile düzelmiyor). Bu, `POST /media`'nin normal
kullanımında karşılaşılabilecek gerçek bir kırıklık.

**İstenen:** İçerik-hash bazlı dedup/kilit mekanizmasının soft-delete sonrası doğru temizlendiğinden
emin olunmalı; ayrıca "concurrent upload" durumu gerçekten geçiciyse, kalıcı hale gelmemesi için
bir zaman aşımı/otomatik kurtarma mekanizması eklenmeli.

---

## 6. Küçük API tasarım notu: `POST /media` `200` dönüyor, `201` beklenir

REST konvansiyonuna göre yeni bir kaynak oluşturan `POST` isteği `201 Created` (+ idealde
`Location` header) dönmeli. `POST /media` şu an `200 OK` dönüyor (diğer `POST` uçları —
`/books`, `/books/{id}/modules`, `/modules/{id}/contents`, `/contents/{id}/blocks`, `/users` —
kontrol etmedim ama muhtemelen tutarlı olması iyi olur). Küçük, öncelik gerektirmeyen bir
tutarlılık notu.

---

## Notlar (aksiyon gerektirmiyor, sadece bilgi)

- `POST /auth/refresh`'i art arda hızlı çağırınca `429 Too Many Requests` alındı — muhtemelen
  kasıtlı rate-limit, sorun değil. Frontend paralel isteklerin tek bir refresh çağrısını
  paylaşmasını sağlayarak buna karşı zaten önlem aldı.
- **Güvenlik denetimi yapıldı (2026-09-01, `security-review` + `api-design` skill'leriyle),
  aşağıdakiler doğrulandı ve sağlam bulundu — aksiyon gerekmiyor:**
  - Login'de brute-force koruması var (birkaç yanlış denemeden sonra `429`)
  - Kullanıcı oluşturmada gerçek şifre politikası uygulanıyor (uzunluk, büyük/küçük harf,
    özel karakter)
  - Medya yüklemede path traversal mümkün değil — dosya adı sadece görüntüleme için tutuluyor,
    gerçek depolama yolu tamamen sunucu tarafında (içerik hash'i) üretiliyor
  - Magic-byte doğrulaması gerçekten çalışıyor — `.png` uzantılı ama içeriği düz metin olan bir
    dosya doğru şekilde reddedildi
  - Bozuk JSON / geçersiz enum değeri gibi girdilerde hata mesajları temiz — stack trace veya
    iç sistem detayı sızdırmıyor
  - Geçersiz tipte route parametresi (`/books/abc/modules/1`) temiz bir `404` veriyor, 500 değil
