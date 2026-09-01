using Isbak_SAR_Guide.Business.Services.Concrete;
using Microsoft.Extensions.Caching.Memory;
using Shouldly;

namespace Isbak_SAR_Guide.Tests.Unit;

/// <summary>
/// 12.2: ISyncCache'in DB'siz, saf davranis testi - set/get/invalidate
/// roundtrip'i. Invalidation'in gercek publish akisiyla eslesmesi
/// (stale veri kalmadigi) ayrica entegrasyon seviyesinde kanitlanir
/// (SyncManifestTests.GetManifest_AfterRepublish_ReturnsFreshVersionNotStale).
/// </summary>
public class MemoryCacheSyncCacheTests
{
    private static MemoryCacheSyncCache CreateCache() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void GetManifest_NothingSet_ReturnsNull()
    {
        var cache = CreateCache();

        cache.GetManifest(bookId: 1).ShouldBeNull();
    }

    [Fact]
    public void SetManifest_ThenGetManifest_ReturnsSameValue()
    {
        var cache = CreateCache();

        cache.SetManifest(bookId: 1, manifestJson: "{\"version\":1}");

        cache.GetManifest(bookId: 1).ShouldBe("{\"version\":1}");
    }

    [Fact]
    public void SetSnapshot_ThenGetSnapshot_ReturnsSameValue()
    {
        var cache = CreateCache();

        cache.SetSnapshot(bookId: 1, snapshotJson: "{\"version\":1}");

        cache.GetSnapshot(bookId: 1).ShouldBe("{\"version\":1}");
    }

    [Fact]
    public void Invalidate_AfterSet_ClearsBothManifestAndSnapshot()
    {
        var cache = CreateCache();
        cache.SetManifest(bookId: 1, manifestJson: "{\"version\":1}");
        cache.SetSnapshot(bookId: 1, snapshotJson: "{\"version\":1}");

        cache.Invalidate(bookId: 1);

        cache.GetManifest(bookId: 1).ShouldBeNull();
        cache.GetSnapshot(bookId: 1).ShouldBeNull();
    }

    [Fact]
    public void SetManifest_DifferentBookIds_DoNotCollide()
    {
        var cache = CreateCache();

        cache.SetManifest(bookId: 1, manifestJson: "{\"version\":1}");
        cache.SetManifest(bookId: 2, manifestJson: "{\"version\":99}");

        cache.GetManifest(bookId: 1).ShouldBe("{\"version\":1}");
        cache.GetManifest(bookId: 2).ShouldBe("{\"version\":99}");
    }
}
