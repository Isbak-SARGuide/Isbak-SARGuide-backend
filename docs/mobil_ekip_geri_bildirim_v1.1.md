# Mobil Ekip Geri Bildirimi — Sync Sözleşmesi v1.1

**Tarih:** 2026-08-28
**İlgili doküman:** Isbak SAR Guide — Mobile Sync Contract v1.1 (2026-08-28 tarihli, gerçek v16 API yanıtlarıyla yenilenen sürüm)
**Hazırlayan:** Mobil ekip (irem)

Sözleşmeyi mobil koda karşı satır satır kontrol ettik. Alan adları, endpoint'ler, hata kodları ve blok tipleri (1-6) mevcut implementasyonumuzla birebir örtüşüyor — bu kısımda herhangi bir uyumsuzluk yok. Aşağıdaki dört madde, kod yazarken netliğe ihtiyaç duyduğumuz veya sözleşmede boşluk gördüğümüz noktalar.

---

## 1. Medya dosyalarının base URL'i belirsiz (öncelikli)

`manifest.media[]` ve `ContentBlock.media` içindeki `url` alanı göreli bir yol olarak geliyor, örneğin:

```
media/kentsel-arama-kurtarma-el-kitabi/sektor-ceyrek-diyagrami.png
```

Sözleşmenin hiçbir yerinde bu yolun önüne hangi host/prefix'in ekleneceği yazmıyor. `/sync` endpoint'lerinin base'i (`/api/v{version}/sync`) ile aynı host mu, yoksa medya için ayrı bir CDN/host mu kullanılıyor? Örnek: `https://{host}/media/...` mi, yoksa `https://{host}/api/v1/media/...` mi, yoksa tamamen farklı bir domain mi?

**Neden önemli:** Bu bilgi olmadan görsel/video indirme özelliğini hiç yazamıyoruz — şu an Konu Detay ekranında görsel blokları "önizleme yakında" yer tutucusuyla gösteriyoruz.

**Önerilen çözüm:** Ya dokümana bir "Medya Base URL" satırı eklenebilir, ya da en pratik çözüm olarak `url` alanı sözleşmeye aykırı olmadan (additive, mevcut alan değişmiyor) tam/mutlak bir URL olarak değiştirilebilir — bu ihtimalde mobilde hiç ek kod gerekmez.

---

## 2. Varyant grubunda "Konular" listesinde hangi title/summary gösterilecek?

§3.2'deki `variantGroupKey`/`variantLabel` mantığını anladık ve mobil tarafta buna göre model güncellemesini yaptık. Ama bir soru açık: bir modülün "Konular" listesinde, aynı `variantGroupKey`'i paylaşan birden fazla content (örn. Temel Düğümler'in F8/F9/TH/ABK varyantları) TEK bir satır olarak gösterilecek — bu satırın `title`/`summary`'si hangi varyanttan alınacak?

Şu an her varyantın kendi `title`'ı farklı (`"Temel Düğümler — 8 Şeklinde Döngü Düğüm"`, muhtemelen F9/TH/ABK için de ayrı başlıklar). `displayOrder`'ı en küçük olan varyantın title/summary'sini mi kullanmalıyız, yoksa gruba ait ayrı bir ortak başlık (örn. sadece `"Temel Düğümler"`) mı planlanıyor?

**Önerilen çözüm:** Ya "en küçük displayOrder'lı varyantın title/summary'si kullanılır" kuralını sözleşmeye bir not olarak ekleyin, ya da grup için ayrı bir `variantGroupTitle` gibi additive bir alan düşünün.

---

## 3. Video/Animation (`type=3`/`4`) için `dataJson`/`media` şeması hiç örneklenmemiş

§4'te yazıyor: *"Kitapta şu an Video/Animation tipinde blok yok"*. Bunu anlıyoruz, ama adım adım anlatılan içerikler (örn. düğüm bağlama, kaldırma teknikleri gibi çoklu adımlı konular) muhtemelen bu tiplerle modellenecek. Şu an elimizde şekil hakkında hiçbir örnek yok:

- Animation için `dataJson` içinde hangi alanlar olacak (adım listesi mi, süre mi, her adımın kendi görseli mi)?
- Video için `media` normal `MediaSummaryDto` mu (yine 1. maddedeki base URL sorusuyla bağlantılı), yoksa ek alanlar (süre, thumbnail) olacak mı?

**Neden önemli:** Bu iki tip gerçek veriyle gelmeden önce mobil tarafta render mantığını doğru tasarlayamıyoruz; şu an sadece "yakında" yer tutucusu gösteriyoruz.

**Önerilen çözüm:** İlk gerçek Video/Animation içeriği yayınlanmadan önce, sentetik de olsa bir örnek `dataJson` paylaşılırsa mobil tarafı önceden hazırlayabiliriz.

---

## 4. (Düşük öncelik) `changes` yanıtında `book` alanı yok

`manifest` ve `snapshot` yanıtları `book` (title/description/slug) taşıyor, ama `changes` (§3.3) taşımıyor — sadece `modules` her zaman toptan geliyor. Kitabın kendi başlığı/açıklaması bir yayında değişirse, mobil bunu delta ile öğrenemez; ancak tam `snapshot` çekerek fark eder.

Bunun muhtemelen kasıtlı olduğunu düşünüyoruz (kitap meta verisi nadiren değişir), sadece teyit etmek istedik — bilerek mi tasarım dışı bırakıldı (§9'daki "bilerek sözleşmeye alınmayanlar" listesine benzer), yoksa gelecekte additive bir `book` alanı `changes`'e de eklenebilir mi?

---

*Not: Bu geri bildirim, sözleşmenin geri kalanının (endpoint'ler, hata kodları, checksum akışı, blok tipleri 1-6) mobil kodla tam uyumlu olduğunu ve herhangi bir değişiklik gerektirmediğini teyit eder niteliktedir — yukarıdaki 4 madde dışında başka bir sorun bulmadık.*
