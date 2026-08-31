# Coklu-asama build: SDK imaji sadece derleme/publish icin - runtime imajinda
# SDK'nin kendisi (derleyici, MSBuild araclari) yer almaz, sadece calisan
# uygulama + ASP.NET Core paylasilan calisma zamani. Bu imaj boyutunu ciddi
# kucultur ve saldiri yuzeyini daraltir.

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Directory.Build.props TUM csproj'lara MSBuild tarafindan otomatik uygulanir
# (TargetFramework dahil - hicbir csproj bunu kendi icinde tekrar tanimlamiyor).
# Kopyalanmazsa restore/publish TargetFramework'suz kalir ve patlar.
COPY Directory.Build.props .

# Once sadece proje dosyalarini kopyala - restore katmani, kaynak kod
# degismedigi surece Docker layer cache'inden gelir (build hizi).
COPY Isbak_SAR_Guide.Backend.slnx .
COPY Isbak_SAR_Guide.API/Isbak_SAR_Guide.API.csproj Isbak_SAR_Guide.API/
COPY Isbak_SAR_Guide.Business/Isbak_SAR_Guide.Business.csproj Isbak_SAR_Guide.Business/
COPY Isbak_SAR_Guide.DataAccess/Isbak_SAR_Guide.DataAccess.csproj Isbak_SAR_Guide.DataAccess/
COPY Isbak_SAR_Guide.Entities/Isbak_SAR_Guide.Entities.csproj Isbak_SAR_Guide.Entities/
RUN dotnet restore Isbak_SAR_Guide.API/Isbak_SAR_Guide.API.csproj

COPY Isbak_SAR_Guide.API/ Isbak_SAR_Guide.API/
COPY Isbak_SAR_Guide.Business/ Isbak_SAR_Guide.Business/
COPY Isbak_SAR_Guide.DataAccess/ Isbak_SAR_Guide.DataAccess/
COPY Isbak_SAR_Guide.Entities/ Isbak_SAR_Guide.Entities/

RUN dotnet publish Isbak_SAR_Guide.API/Isbak_SAR_Guide.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Storage:BasePath prod'da /storage'a (compose.prod.yaml'daki api_storage
# volume'unun mount noktasi) mutlak yolla set edilir - bkz. compose.prod.yaml
# + docs/Deployment.md. root DISINDA bir kullaniciyla mount edilen, henuz
# icinde veri olmayan bir named volume'un mount noktasini Docker varsayilan
# olarak root:root sahipliginde OLUSTURUR - USER app'e gecmeden ONCE burada
# elle olusturup chown etmezsek, app kullanicisi ilk medya yuklemesinde
# (LocalFileStorageService.UploadAsync -> Directory.CreateDirectory alt
# klasoru) yazma izni hatasi alir. Health check'ler bunu YAKALAMAZ (Storage
# kok klasoru zaten var, sadece ALT klasor olusturma an'inda patlar).
RUN mkdir -p /storage && chown app:app /storage

# aspnet:10.0 (Ubuntu tabanli) curl/wget ICERMIYOR - compose.prod.yaml'daki
# HEALTHCHECK (container-ici "curl -f http://localhost:8080/health") curl
# olmadan calisamaz ve container surekli "unhealthy" raporlar (canli
# compose testinde dogrulandi). libfontconfig1: SkiaSharp'in native paylasimli
# kutuphanesi (libSkiaSharp.so, Faz 12.7 WebP+thumbnail icin eklendi) Linux'ta
# bunu dinamik olarak baglar - eksikse MediaService.UploadAsync'in ilk
# cagrisinda "Unable to load shared library 'libSkiaSharp'" ile patlar, health
# check bunu YAKALAMAZ (sadece ilk medya yuklemesi aninda ortaya cikar, Faz
# 8'deki /storage izin sorunuyla ayni sinif hata - dotnet build/test hicbir
# zaman gormez). --no-install-recommends + apt cache temizligi ile ek yuk
# minimumda tutuluyor.
RUN apt-get update && apt-get install -y --no-install-recommends curl libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*

# Root olarak calistirmiyoruz - imaj zaten "app" adinda ayricaliksiz bir
# kullanici iceriyor (mcr.microsoft.com/dotnet/aspnet resmi imaj deseni).
USER app

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "Isbak_SAR_Guide.API.dll"]
