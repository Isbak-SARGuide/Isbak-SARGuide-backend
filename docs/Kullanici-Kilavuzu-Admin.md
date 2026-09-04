# Admin/Editör Kullanıcı Kılavuzu

Bu kılavuz, CMS (içerik yönetim) panelini kullanan **Admin** ve **Editör** kullanıcıları içindir. Teknik bir doküman değildir — panelde günlük olarak yapacağınız işlemleri anlatır.

> Bu kılavuz panelin *davranışını* anlatır; panelin kendisi (butonlar, ekranlar) ayrı bir web projesinde (`Isbak-SARGuide-web`, React) geliştirilir. Buradaki her adım, backend'in gerçekte nasıl çalıştığına dayanır.

## İçindekiler

1. [Roller: Admin ve Editör Arasındaki Fark](#roller-admin-ve-editör-arasındaki-fark)
2. [Kitap, Kategori ve İçerik Yapısı](#kitap-kategori-ve-i̇çerik-yapısı)
3. [İçerik Ekleme ve Sıralama](#i̇çerik-ekleme-ve-sıralama)
4. [Medya (Görsel) Yükleme](#medya-görsel-yükleme)
5. [Yayınlama — "Yayınla" Ne Yapar?](#yayınlama--yayınla-ne-yapar)
6. [Önizleme — Yayınlamadan Önce Ne Değişecek?](#önizleme--yayınlamadan-önce-ne-değişecek)
7. [Eski Bir Sürüme Dönme (Rollback)](#eski-bir-sürüme-dönme-rollback)
8. [Kullanıcı Yönetimi](#kullanıcı-yönetimi)
9. [Sık Karşılaşılan Durumlar](#sık-karşılaşılan-durumlar)

---

## Roller: Admin ve Editör Arasındaki Fark

| Yapabilir | Editör | Admin |
|---|:---:|:---:|
| Kategori/içerik ekle, düzenle, sil, sırala | ✅ | ✅ |
| Medya (görsel) yükle/sil | ✅ | ✅ |
| Kendi şifresini değiştir | ✅ | ✅ |
| **Yayınla** | ❌ | ✅ |
| **Eski sürüme dön (rollback)** | ❌ | ✅ |
| Yayın geçmişini görüntüle | ❌ | ✅ |
| Kullanıcı ekle/sil/rol değiştir | ❌ | ✅ |

**Kısaca:** Editör içeriği hazırlar, sadece Admin sahaya gönderir. Bu, yanlışlıkla yayınlanmış bir hatanın önüne geçmek için bilinçli bir tasarım — içerik düzenleme yetkisiyle "sahaya ne gider" yetkisi ayrılmış durumda.

## Kitap, Kategori ve İçerik Yapısı

Panelde üç seviye vardır:

```
Kitap
 └─ Kategori (Modül)     — örn. "Kaldırma & Taşıma"
     └─ Konu (İçerik)     — örn. "Temel Düğümler"
         └─ Blok           — metin, görsel, tablo, uyarı kutusu
```

> **Şu an alt kategori desteklenmiyor.** Bir kategorinin altına başka bir kategori ekleyemezsiniz (e-ticaret sitelerindeki gibi bir ağaç yapısı yok) — sadece tek seviyeli kategori listesi var, her kategorinin altında doğrudan konular bulunur. Bu, planlanan ama henüz uygulanmamış bir gelecek özelliği.

## İçerik Ekleme ve Sıralama

Yeni bir kategori veya konu eklediğinizde, sırası otomatik olarak **listenin sonuna** eklenir — sıra numarasını elle girmenize gerek yoktur.

**Sürükle-bırak ile sıralama:** Panelde bir kategoriyi veya konuyu sürükleyip bıraktığınızda, sıra anında kaydedilir. Bir kayıt silindiğinde, kalan kardeşlerin sırası **otomatik olarak yeniden numaralandırılır** — aralarda boşluk kalmaz.

**Silinen bir kategori/konunun altındakiler ne olur?** Bir kategoriyi sildiğinizde, altındaki konular otomatik silinmez — panelde görünmez olurlar (artık ulaşılamazlar) ama teknik olarak veritabanında dururlar. Aynı şey bir konu silindiğinde altındaki bloklar için de geçerli.

## Medya (Görsel) Yükleme

- Desteklenen formatlar: **PNG, JPEG, GIF, WEBP**. Video/animasyon dosyaları şu an desteklenmiyor (yüklemeye çalışırsanız hata alırsınız).
- Yüklediğiniz her görsel, mobil cihazlarda daha hızlı yüklensin diye otomatik olarak **WebP** formatına çevrilir — **GIF hariç**: animasyonlu bir GIF yüklerseniz, animasyonu kaybetmemesi için olduğu gibi saklanır.
- Aynı dosyayı iki kez yüklerseniz, sistem bunu fark eder ve tekrar depolamaz (aynı kayıt geri döner) — depolama alanı israf edilmez.
- Bir görseli, onu kullanan bir blok varken silmeye çalışırsanız sistem sizi uyarır — önce o bloğu güncelleyin veya silin.

## Yayınlama — "Yayınla" Ne Yapar?

Panelde yaptığınız her değişiklik (içerik ekleme, düzenleme, sıralama) önce bir **taslak**tır. Mobil uygulamayı kullanan saha personeli, siz **"Yayınla"** demeden bu değişiklikleri **görmez**.

"Yayınla" dediğinizde:
1. O anki taslağın tam bir anlık görüntüsü alınır.
2. Bu anlık görüntü, kalıcı ve **değiştirilemez** bir "sürüm" olarak saklanır (sürüm numarası bir artar).
3. Mobil cihazlar bir sonraki senkronizasyonlarında sadece **gerçekten değişen** kısmı indirir — tüm kitabı yeniden indirmezler.

**Hiçbir şey değişmediyse** "Yayınla"ya basmak yeni bir sürüm oluşturmaz — sistem bunu fark edip mevcut sürümü aynen bırakır.

## Önizleme — Yayınlamadan Önce Ne Değişecek?

"Yayınla" butonundan önce bir **önizleme** ekranı vardır — hiçbir şeyi kalıcı olarak değiştirmeden, şu an yayınlasanız neyin **eklenece**ğini, **değişece**ğini ve **kaldırılaca**ğını gösterir. Bu, yanlışlıkla boş veya eksik bir yayın yapmamak için eklendi (kullanıcı geri bildirimiyle: eskiden "Yayınla" hiçbir geri bildirim vermeden direkt yayınlıyordu).

## Eski Bir Sürüme Dönme (Rollback)

Yanlışlıkla yayınlanmış bir hatayı düzeltmek için, geçmişteki bir sürüme geri dönebilirsiniz (sadece **Admin**).

**Önemli — rollback'in tam olarak ne yaptığını anlamak kritik:**

- Rollback, geçmişi **silmez**. Eski içeriği, **yeni bir sürüm numarasıyla** yeniden yayınlar (git'teki "revert" gibi — eski hâle dönüyor ama yeni bir "kayıt" olarak).
- Rollback, **panelinizdeki taslağa dokunmaz.** Sadece mobilin gördüğü yayınlanmış veriyi eski hâline döndürür. Panelde hâlâ rollback öncesi hâliyle (örn. yanlışlıkla eklenmiş bir kategori) duruyor olacaktır.
- **Bu yüzden:** Rollback yaptıktan sonra tekrar normal **"Yayınla"**ya basarsanız, panel yine güncel taslağı yayınlar — rollback'in etkisini geri almış olursunuz! Rollback sonrası ekstra bir "Yayınla" yapmanıza **gerek yoktur**, rollback zaten kendi başına bir yayındır.
- Bir hatayı kalıcı olarak (hem mobilden hem panelden) kaldırmak istiyorsanız, rollback yeterli değildir — ilgili kategori/içeriği panelden de gerçekten silmeniz gerekir.

## Kullanıcı Yönetimi

Sadece **Admin** yeni kullanıcı ekleyebilir — self-servis kayıt yoktur, her hesap bir Admin tarafından açılır.

- Kullanıcı silme **kalıcıdır** (geri alınamaz) — "pasifleştirme" değil, gerçek silme.
- **Admin hesapları silinemez.** Bir Admin'i silmeye çalışırsanız hata alırsınız — bu, yanlışlıkla panele erişimi kimsenin kalmadığı bir duruma düşmemek için bilinçli bir kısıt.
- Sadece Editör hesapları silinebilir.

## Sık Karşılaşılan Durumlar

**"Yayınladım ama mobilde görünmüyor."**
Mobil uygulama, siz "Yayınla" dediğinizde otomatik olarak haberdar olmaz — bir sonraki senkronizasyonunda görür. Mobil uygulama sadece **açılışta** (uygulama tamamen kapatılıp yeniden açıldığında) senkronize olur; uygulamayı arka planda bırakıp geri dönmek yeterli değildir. Test ederken mobil uygulamayı tamamen kapatıp yeniden açın.

**"Sıralamayı değiştirdim ama mobilde eski sıra görünüyor."**
Sıralama değişikliği de bir taslak değişikliğidir — mobilde görünmesi için **Yayınla** gerekir. (Bkz. yukarıdaki senkronizasyon notu da geçerli.)

**"Bir görseli sildim ama telefon depolamasında hâlâ duruyor."**
Bilinen bir sınırlama — mobil uygulama şu an silinen medya dosyalarını cihazdan otomatik temizlemiyor (bkz. [`Kullanici-Kilavuzu-Saha.md`](Kullanici-Kilavuzu-Saha.md)).

---

*Teknik API detayları için → [`CMS-API-Sozlesmesi.md`](CMS-API-Sozlesmesi.md). Mimari kararların gerekçesi için → [`Mimari.md`](Mimari.md).*
