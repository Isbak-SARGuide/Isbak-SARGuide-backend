# Deployment

Bu belge, prod imajini/compose stack'ini ayaga kaldirmak icin gereken ortam
degiskenlerini ve CD is akisini belgeler. Kapsam bilerek dar: imaj
build+push (`ghcr.io`) burada var, gercek bir canli sunucuya deploy adimi
**yok** — projede henuz bir hosting hedefi kararlastirilmadi.

## Imaj

`Dockerfile` coklu-asama: `dotnet/sdk:10.0` ile derle, `dotnet/aspnet:10.0`
uzerinde calistir (root olmayan `app` kullanicisi, `dotnet publish` ciktisi
disinda hicbir kaynak kopyalanmaz). `docker build -t isbak-sar-guide-api .`
repo kokunden calisir.

## CD (`.github/workflows/cd.yml`)

`main`'e her push'ta (yani her merge'de) tetiklenir, imaji `ghcr.io/<repo>`
altina iki etiketle push eder: `latest` ve `<commit-sha>` (rollback/izlenebilirlik
icin sabit bir referans). Kimlik dogrulama `secrets.GITHUB_TOKEN` ile otomatik —
ek bir PAT/secret kurulumu gerekmez. **Bu workflow hicbir sunucuya baglanmaz,
sadece registry'ye push eder.**

## Ortam degiskenleri

Prod compose (`compose.prod.yaml`) bunlarin cogunu `${VAR:?...}` ile ZORUNLU
kilar — eksikse `docker compose up` acik bir hatayla durur, sessizce yanlis
bir varsayimla ayaga kalkmaz. Hepsi `.env` dosyasindan (repo'ya girmez,
`.gitignore`) veya orkestrasyonun kendi secret mekanizmasindan okunur.

| Degisken | Zorunlu | Aciklama |
|---|---|---|
| `POSTGRES_DB` | Hayir (varsayilan `isbak_sar_guide`) | Postgres veritabani adi |
| `POSTGRES_USER` | Hayir (varsayilan `postgres`) | Postgres kullanici adi |
| `POSTGRES_PASSWORD` | **Evet** | Postgres sifresi — hem `postgres` hem `api` servisi kullanir |
| `JWT_ISSUER` | **Evet** | `Jwt:Issuer` — token'i kimin urettigi |
| `JWT_AUDIENCE` | **Evet** | `Jwt:Audience` — token'in kimin icin gecerli oldugu |
| `JWT_SECRET_KEY` | **Evet** | `Jwt:SecretKey` — en az 32 karakter, `appsettings.Development.json`'daki degerin AYNISI ASLA prod'da kullanilmaz |
| `JWT_EXPIRY_MINUTES` | Hayir (varsayilan `60`) | Access token omru (dakika) |
| `JWT_REFRESH_TOKEN_EXPIRY_DAYS` | Hayir (varsayilan `14`) | Refresh token omru (gun) |
| `CORS_ALLOWED_ORIGIN_0` | **Evet** | Admin web dashboard'un origin'i (orn. `https://admin.example.com`) — birden fazla origin icin compose'a `CORS_ALLOWED_ORIGIN_1`, `_2` ... ekleyip `Cors__AllowedOrigins__1` gibi ek satirlar eklenir |
| `STORAGE_MAX_FILE_SIZE_BYTES` | Hayir (varsayilan `20971520` = 20 MB) | Medya upload boyut siniri |
| `STORAGE_ORPHAN_GRACE_HOURS` | Hayir (varsayilan `24`) | Orphan medya temizligi oncesi bekleme suresi |
| `LOGIN_RATE_LIMIT_PERMIT` | Hayir (varsayilan `5`) | `/auth/login` icin IP basina pencere basina izin verilen istek |
| `LOGIN_RATE_LIMIT_WINDOW_SECONDS` | Hayir (varsayilan `60`) | Yukaridaki pencerenin uzunlugu (saniye) |
| `GLOBAL_RATE_LIMIT_PERMIT` | Hayir (varsayilan `300`) | TUM endpoint'ler icin IP basina pencere basina istek limiti (Faz 12.8) — ozellikle `/sync/*` gibi `AllowAnonymous` uclari icin taban koruma; login/refresh ayrica kendi daha siki limitine de tabidir |
| `GLOBAL_RATE_LIMIT_WINDOW_SECONDS` | Hayir (varsayilan `60`) | Yukaridaki pencerenin uzunlugu (saniye) |

`Storage__BasePath` compose icinde sabit (`/storage`, konteyner-ici mutlak
yol + kalici volume) — ortam degiskeni olarak disari acilmadi, degistirmek
volume mount'unu da degistirmeyi gerektirir.

## CORS — izin verilmeyen origin sessizce basarisiz olur

CORS zaten koda gomulu DEGIL: `Program.cs` `Cors:AllowedOrigins` config
bolumunden okur (appsettings'te bir dizi; prod'da `CORS_ALLOWED_ORIGIN_0`/
`_1`/`_2`... env degiskenleriyle doldurulur, yukaridaki tabloya bakin).
Allowlist'te olmayan bir origin'den gelen istek icin taraycini CORS hatasi
SESSIZCE kalir (tarayici standardi, backend'in degistirebilecegi bir sey
degil) — network sekmesinde istek "basarili" (200) gorunebilirken konsolda
CORS hatasi cikmasi, ilk bakista "backend calisiyor, frontend bozuk"
izlenimi verebilir. Deploy oncesi prod domain'i allowlist'e eklemeyi
unutmayin; yeni bir dev/preview portu (Vite'in `5174`, `vite preview`'in
`4173` gibi) eklendiginde de `appsettings.Development.json`'a eklenmesi
gerekir.

## Onune reverse proxy/load balancer eklenirse (henuz yok)

Su anki `compose.prod.yaml` API container'ini dogrudan disari aciyor
(`8080:8080`), reverse proxy yok. Hem login hem global rate limiter
(`Program.cs`, Faz 9.3 + Faz 12.8) IP'yi `RemoteIpAddress`'ten okuyor — bu,
dogrudan-erisimde doğru calisir. **Onune bir reverse proxy/load balancer/CDN
konursa**, ASP.NET Core'un `UseForwardedHeaders()` middleware'i (proxy'nin
IP'sine `KnownProxies`/`KnownNetworks` ile kisitlanmis) eklenmeden rate
limiter'lerin butun trafigi proxy'nin tek IP'sinden geliyormus gibi gorup
TUM kullanicilar icin PAYLASIMLI tek bir limit havuzuna dusecegini, yani hem
login brute-force korumasinin (5 deneme/60sn, IP basina) hem `/sync/*` icin
taban korumanin (300 istek/60sn, IP basina) sessizce devre disi kalacagini
unutma.

## `docker compose up`

```bash
# .env dosyasi olustur (repo kokunde, git'e girmez)
cp .env.example .env   # yoksa yukaridaki tablodan elle doldur
docker compose -f compose.prod.yaml up -d --build

# Health kontrolu
curl http://localhost:8080/health         # liveness - bagimlilik kontrol etmez
curl http://localhost:8080/health/ready   # readiness - Postgres baglantisini da kontrol eder
```

M8 kapanis kaniti: `docker compose -f compose.prod.yaml up` ile prod imaji
ayakta ve her iki health ucu da `Healthy` donuyor olmali.
