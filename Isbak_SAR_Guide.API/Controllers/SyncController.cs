using Asp.Versioning;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

/// <summary>
/// Mobil uygulamanin tek temas noktasi. Tamami [AllowAnonymous] - mobil hic
/// kimlik dogrulamasi yapmiyor (plan belgesi Bolum 1, "Mobil kimlik dogrulama: Yok").
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[AllowAnonymous]
[Route("api/v{version:apiVersion}/[controller]")]
public class SyncController(ISyncService syncService) : ControllerBase
{
    [HttpGet("manifest")]
    public async Task<IActionResult> GetManifest([FromQuery] int bookId, CancellationToken cancellationToken)
    {
        var result = await syncService.GetManifestAsync(bookId, cancellationToken);
        // Verbatim: ManifestJson kolonu deserialize edilmeden aynen gecirilir.
        // ETag = bookId + govdedeki "version" - yayin immutable oldugu icin
        // ayni versiyon = ayni govde (12.4).
        return result.ToJsonContentResultWithETag(
            this, json => $"{bookId}.{ResultExtensions.ExtractJsonIntProperty(json, "version")}");
    }

    [HttpGet("snapshot")]
    public async Task<IActionResult> GetSnapshot([FromQuery] int bookId, CancellationToken cancellationToken)
    {
        var result = await syncService.GetSnapshotAsync(bookId, cancellationToken);
        // Verbatim: SnapshotJson kolonu deserialize edilmeden aynen gecirilir -
        // istemci SHA256(govde) == manifest.checksum dogrulamasi yapar.
        // ETag manifest'le ayni semadan turetilir (bookId + version) - snapshot
        // govdesinin kendi ust-seviye checksum alani yok, versiyon zaten tek
        // basina govdeyi tekil olarak belirliyor (immutable publication).
        return result.ToJsonContentResultWithETag(
            this, json => $"{bookId}.{ResultExtensions.ExtractJsonIntProperty(json, "version")}");
    }

    [HttpGet("changes")]
    public async Task<IActionResult> GetChanges(
        [FromQuery] int bookId,
        [FromQuery] int fromVersion,
        CancellationToken cancellationToken)
    {
        var result = await syncService.GetChangesAsync(bookId, fromVersion, cancellationToken);
        // Verbatim zarf: envelope elle yazilir, content/modul/medya parcalari ham kopyalanir.
        // ETag = bookId + fromVersion + govdedeki "toVersion" - immutable
        // yayin defterinde bu ucu (bookId, fromVersion, toVersion) her zaman
        // ayni delta govdesini uretir.
        return result.ToJsonContentResultWithETag(
            this, json => $"{bookId}.{fromVersion}-{ResultExtensions.ExtractJsonIntProperty(json, "toVersion")}");
    }
}
