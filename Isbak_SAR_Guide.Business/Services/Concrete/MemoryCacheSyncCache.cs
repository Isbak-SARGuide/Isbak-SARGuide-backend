using Isbak_SAR_Guide.Business.Services.Abstract;
using Microsoft.Extensions.Caching.Memory;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

/// <summary>
/// ISyncCache'in tek instance'lik dagitima uygun implementasyonu (12.2).
/// Redis/dagitik cache bilerek yok - Dockerfile/compose.prod.yaml tek API
/// container'i calistiriyor, dagitik cache bu olcekte gereksiz karmasiklik
/// olurdu (roadmap'in kendi 12.1 olcumu zaten cache'i YAGNI bulmustu; bu
/// sinif yine de kullanici onayiyla eklendi - IStorageService/Strategy
/// deseniyle ayni sekilde, ileride Redis'e gecis tek yeni sinif olur).
/// </summary>
public class MemoryCacheSyncCache(IMemoryCache cache) : ISyncCache
{
    /// <summary>
    /// Savunma amacli guvenlik agi - invalidation event-driven (FinalizeAsync)
    /// oldugu icin normal akista hic dolmadan once Invalidate cagirilir. Bu
    /// sadece bir invalidation cagrisi kacirilirsa (ornegin ileride cok-
    /// instance bir deploy'da) veriyi sonsuza kadar bayat tutmamak icin.
    /// </summary>
    private static readonly TimeSpan _safetyNetTtl = TimeSpan.FromMinutes(30);

    public string? GetManifest(int bookId) =>
        cache.TryGetValue(ManifestKey(bookId), out string? value) ? value : null;

    public string? GetSnapshot(int bookId) =>
        cache.TryGetValue(SnapshotKey(bookId), out string? value) ? value : null;

    public void SetManifest(int bookId, string manifestJson) =>
        cache.Set(ManifestKey(bookId), manifestJson, _safetyNetTtl);

    public void SetSnapshot(int bookId, string snapshotJson) =>
        cache.Set(SnapshotKey(bookId), snapshotJson, _safetyNetTtl);

    public void Invalidate(int bookId)
    {
        cache.Remove(ManifestKey(bookId));
        cache.Remove(SnapshotKey(bookId));
    }

    private static string ManifestKey(int bookId) => $"sync:manifest:{bookId}";

    private static string SnapshotKey(int bookId) => $"sync:snapshot:{bookId}";
}
