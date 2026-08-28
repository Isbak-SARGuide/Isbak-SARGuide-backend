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

# Root olarak calistirmiyoruz - imaj zaten "app" adinda ayricaliksiz bir
# kullanici iceriyor (mcr.microsoft.com/dotnet/aspnet resmi imaj deseni).
USER app

COPY --from=build /app/publish .

# Storage:BasePath varsayilani "../storage" (API'nin content root'una gore
# GORELI - bkz. appsettings.Development.json). Container'da content root
# /app oldugu icin bu, konteynerin KOKUNE (/storage) cikar - kasitli degil,
# appsettings.Development.json'daki yerel-gelistirme varsayimindan miras.
# Prod compose bunu VOLUME MOUNT'LU, mutlak bir yola (Storage__BasePath)
# ORDE EDEREK gecersiz kilar - bkz. compose.prod.yaml + docs/Deployment.md.
EXPOSE 8080
ENTRYPOINT ["dotnet", "Isbak_SAR_Guide.API.dll"]
