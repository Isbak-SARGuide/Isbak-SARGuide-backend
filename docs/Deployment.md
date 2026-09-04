# Deployment

Bu belge, prod imajını/compose stack'ini ayağa kaldırmak için gereken ortam
değişkenlerini ve CD iş akışını belgeler. Kapsam bilerek dar: imaj build+push
(`ghcr.io`) burada var, gerçek bir canlı sunucuya deploy adımı **yok** — projede henüz
bir hosting hedefi kararlaştırılmadı.

## İmaj

`Dockerfile` çoklu-aşama: `dotnet/sdk:10.0` ile derle, `dotnet/aspnet:10.0` üzerinde
çalıştır (root olmayan `app` kullanıcısı, `dotnet publish` çıktısı dışında hiçbir
kaynak kopyalanmaz). `docker build -t isbak-sar-guide-api .` repo kökünden çalışır.

## CD (`.github/workflows/cd.yml`)

`main`'e her push'ta (yani her merge'de) tetiklenir, imajı `ghcr.io/<repo>` altına iki
etiketle push eder: `latest` ve `<commit-sha>` (rollback/izlenebilirlik için sabit bir
referans). Kimlik doğrulama `secrets.GITHUB_TOKEN` ile otomatik — ek bir PAT/secret
kurulumu gerekmez. **Bu workflow hiçbir sunucuya bağlanmaz, sadece registry'ye push
eder.**

## Ortam Değişkenleri

Prod compose (`compose.prod.yaml`) bunların çoğunu `${VAR:?...}` ile ZORUNLU kılar —
eksikse `docker compose up` açık bir hatayla durur, sessizce yanlış bir varsayımla
ayağa kalkmaz. Hepsi `.env` dosyasından (repo'ya girmez, `.gitignore`) veya
orkestrasyonun kendi secret mekanizmasından okunur.

| Değişken | Zorunlu | Açıklama |
|---|---|---|
| `POSTGRES_DB` | Hayır (varsayılan `isbak_sar_guide`) | Postgres veritabanı adı |
| `POSTGRES_USER` | Hayır (varsayılan `postgres`) | Postgres kullanıcı adı |
| `POSTGRES_PASSWORD` | **Evet** | Postgres şifresi — hem `postgres` hem `api` servisi kullanır |
| `JWT_ISSUER` | **Evet** | `Jwt:Issuer` — token'i kimin ürettiği |
| `JWT_AUDIENCE` | **Evet** | `Jwt:Audience` — token'in kimin için geçerli olduğu |
| `JWT_SECRET_KEY` | **Evet** | `Jwt:SecretKey` — en az 32 karakter, `appsettings.Development.json`'daki değerin AYNISI ASLA prod'da kullanılmaz |
| `JWT_EXPIRY_MINUTES` | Hayır (varsayılan `60`) | Access token ömrü (dakika) |
| `JWT_REFRESH_TOKEN_EXPIRY_DAYS` | Hayır (varsayılan `14`) | Refresh token ömrü (gün) |
| `CORS_ALLOWED_ORIGIN_0` | **Evet** | Admin web dashboard'un origin'i (örn. `https://admin.example.com`) — birden fazla origin için compose'a `CORS_ALLOWED_ORIGIN_1`, `_2` ... ekleyip `Cors__AllowedOrigins__1` gibi ek satırlar eklenir |
| `STORAGE_MAX_FILE_SIZE_BYTES` | Hayır (varsayılan `20971520` = 20 MB) | Medya upload boyut sınırı |
| `STORAGE_ORPHAN_GRACE_HOURS` | Hayır (varsayılan `24`) | Orphan medya temizliği öncesi bekleme süresi |
| `LOGIN_RATE_LIMIT_PERMIT` | Hayır (varsayılan `5`) | `/auth/login` için IP başına pencere başına izin verilen istek |
| `LOGIN_RATE_LIMIT_WINDOW_SECONDS` | Hayır (varsayılan `60`) | Yukarıdaki pencerenin uzunluğu (saniye) |
| `GLOBAL_RATE_LIMIT_PERMIT` | Hayır (varsayılan `300`) | TÜM endpoint'ler için IP başına pencere başına istek limiti — özellikle `/sync/*` gibi `AllowAnonymous` uçları için taban koruma; login/refresh ayrıca kendi daha sıkı limitine de tabidir |
| `GLOBAL_RATE_LIMIT_WINDOW_SECONDS` | Hayır (varsayılan `60`) | Yukarıdaki pencerenin uzunluğu (saniye) |

`Storage__BasePath` compose içinde sabit (`/storage`, konteyner-içi mutlak yol +
kalıcı volume) — ortam değişkeni olarak dışarı açılmadı, değiştirmek volume mount'unu
da değiştirmeyi gerektirir.

## CORS — izin verilmeyen origin sessizce başarısız olur

CORS zaten koda gömülü DEĞİL: `Program.cs` `Cors:AllowedOrigins` config bölümünden
okur (appsettings'te bir dizi; prod'da `CORS_ALLOWED_ORIGIN_0`/`_1`/`_2`... env
değişkenleriyle doldurulur, yukarıdaki tabloya bakın). Allowlist'te olmayan bir
origin'den gelen istek için tarayıcı CORS hatası SESSİZCE kalır (tarayıcı standardı,
backend'in değiştirebileceği bir şey değil) — network sekmesinde istek "başarılı"
(200) görünebilirken konsolda CORS hatası çıkması, ilk bakışta "backend çalışıyor,
frontend bozuk" izlenimi verebilir. Deploy öncesi prod domain'i (web admin panelinin
gerçek adresi) allowlist'e eklemeyi unutmayın; yeni bir dev/preview portu eklendiğinde
de `appsettings.Development.json`'a eklenmesi gerekir.

## Önüne reverse proxy/load balancer eklenirse (henüz yok)

Şu anki `compose.prod.yaml` API container'ını doğrudan dışarı açıyor (`8080:8080`),
reverse proxy yok. Hem login hem global rate limiter (`Program.cs`) IP'yi
`RemoteIpAddress`'ten okuyor — bu, doğrudan-erişimde doğru çalışır. **Önüne bir
reverse proxy/load balancer/CDN konursa**, ASP.NET Core'un `UseForwardedHeaders()`
middleware'i (proxy'nin IP'sine `KnownProxies`/`KnownNetworks` ile kısıtlanmış)
eklenmeden rate limiter'lerin bütün trafiği proxy'nin tek IP'sinden geliyormuş gibi
görüp TÜM kullanıcılar için PAYLAŞIMLI tek bir limit havuzuna düşeceğini, yani hem
login brute-force korumasının (5 deneme/60sn, IP başına) hem `/sync/*` için taban
korumanın (300 istek/60sn, IP başına) sessizce devre dışı kalacağını unutmayın.

## `docker compose up`

```bash
# .env dosyası oluştur (repo kökünde, git'e girmez)
cp .env.example .env   # yoksa yukarıdaki tablodan elle doldur
docker compose -f compose.prod.yaml up -d --build

# Health kontrolü
curl http://localhost:8080/health         # liveness - bağımlılık kontrol etmez
curl http://localhost:8080/health/ready   # readiness - Postgres bağlantısını da kontrol eder
```

Prod imajının çalıştığının kanıtı: `docker compose -f compose.prod.yaml up` ile prod
imajı ayakta ve her iki health ucu da `Healthy` dönüyor olmalı.

---

*Mimari kararların gerekçesi için → [`Mimari.md`](Mimari.md).*
