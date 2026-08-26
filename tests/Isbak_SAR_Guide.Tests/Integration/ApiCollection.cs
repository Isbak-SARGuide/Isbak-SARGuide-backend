using Xunit;

namespace Isbak_SAR_Guide.Tests.Integration;

/// <summary>
/// Bu isimle isaretlenen (bkz. [Collection("Api")]) tum test siniflari AYNI
/// ApiFactory ornegini (ve dolayisiyla ayni Postgres container'ini) paylasir.
/// Aksi halde xUnit her test sinifi icin ayri bir fixture olusturur - her
/// sinif kendi container'ini acar, testler yavaslar.
/// </summary>
[CollectionDefinition("Api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
