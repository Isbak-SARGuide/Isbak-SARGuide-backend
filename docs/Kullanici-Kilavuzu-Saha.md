# Saha Personeli Kullanıcı Kılavuzu (Mobil Uygulama)

Bu kılavuz, sahada mobil uygulamayı kullanan arama-kurtarma personeli içindir.

> Mobil uygulamanın kendisi ayrı bir projede (Flutter) geliştirilir; bu kılavuz, o uygulamanın backend ile nasıl çalıştığını ve sahada nelere dikkat etmeniz gerektiğini anlatır.

## İçindekiler

1. [Uygulama Nasıl Çalışır — Kısaca](#uygulama-nasıl-çalışır--kısaca)
2. [İlk Kurulum](#i̇lk-kurulum)
3. [İnternetsiz (Offline) Kullanım](#i̇nternetsiz-offline-kullanım)
4. [Güncel İçeriği Görmek İçin Ne Yapmalısınız](#güncel-i̇çeriği-görmek-i̇çin-ne-yapmalısınız)
5. [İçerik Arama](#i̇çerik-arama)
6. [Bilinen Sınırlamalar](#bilinen-sınırlamalar)

---

## Uygulama Nasıl Çalışır — Kısaca

Uygulama, el kitabının tamamını **telefonunuza indirir** ve internet olmasa bile çalışır. İnternet sadece kitap güncellendiğinde yeni içeriği indirmek için gerekir — sahada, internetsiz bir ortamda, elinizdeki en son indirilmiş içeriği okumaya devam edebilirsiniz.

## İlk Kurulum

Uygulamayı ilk açtığınızda, internet bağlantısı gerekir — kitabın **tamamı** bir kere indirilir (görseller dahil). Bu indirme tamamlanana kadar bekleyin; sonrasında internetsiz kullanabilirsiniz.

## İnternetsiz (Offline) Kullanım

İlk indirme tamamlandıktan sonra, uygulama **tamamen internetsiz** çalışır:
- Tüm konular, görseller ve tablolar telefonunuzda saklıdır.
- Arama, gezinme, okuma — hiçbiri internet gerektirmez.

## Güncel İçeriği Görmek İçin Ne Yapmalısınız

**Önemli:** Uygulama, arka planda bekletildiğinde veya açık kaldığında **otomatik olarak** yeni içerik kontrol etmez. Yeni bir kitap güncellemesi olup olmadığı **sadece uygulama tamamen kapatılıp yeniden açıldığında** (soğuk başlangıç) kontrol edilir.

**Yani:** Ekip liderinizden veya yöneticinizden "kitap güncellendi" haberini aldıysanız:
1. Uygulamayı görev yöneticisinden **tamamen kapatın** (sadece ana ekrana dönmek yeterli değildir).
2. Uygulamayı tekrar açın.
3. İnternet varsa, açılışta otomatik olarak güncel içerik indirilir.

İnternet yoksa uygulama elinizdeki son indirilmiş içerikle sorunsuz açılır — güncelleme kontrolü başarısız olursa hiçbir hata göstermez, sessizce mevcut veriyle devam eder.

## İçerik Arama

Arama, telefonunuzda indirilmiş içerik üzerinde çalışır (internet gerektirmez) — konu başlıklarında, özetlerde ve içerik metinlerinde arar.

## Bilinen Sınırlamalar

- **Video/Animasyon içerikler henüz yok** — el kitabı şu an sadece metin, görsel, tablo ve uyarı kutusu formatlarını destekliyor.
- **Silinen görseller telefonda kalabilir.** Bir görsel backend'de kaldırılsa bile, daha önce indirilmişse telefonunuzun deposunda kalmaya devam edebilir (bilinen bir sınırlama, gelecekte düzeltilecek). Depolama alanı sorun olursa, uygulamayı silip yeniden kurmak bunu temizler.
- **Arka planda otomatik senkronizasyon yok.** Yukarıda anlatıldığı gibi, güncel içerik almak için uygulamayı yeniden başlatmanız gerekir.

---

*Backend'in senkronizasyon mekanizmasının teknik detayları için → [`Sync-Sozlesmesi.md`](Sync-Sozlesmesi.md). Mobil uygulamanın kendisi ayrı bir repoda geliştirilir: `Isbak-SARGuide-mobile` (Flutter).*
