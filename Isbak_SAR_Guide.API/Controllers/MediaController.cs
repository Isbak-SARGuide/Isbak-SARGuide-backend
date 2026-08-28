using Asp.Versioning;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/media")]
public class MediaController(IMediaService mediaService) : ControllerBase
{
    [HttpPost]
    [Consumes("multipart/form-data")]
    // Kestrel/form icin sert bir tavan - asil (yapilandirilabilir,
    // StorageOptions.MaxFileSizeBytes'tan okunan) sinir MediaService'te.
    [RequestSizeLimit(52_428_800)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
        {
            return Problem(detail: "Dosya boş olamaz.", statusCode: StatusCodes.Status400BadRequest, title: "Media.Empty");
        }

        await using var stream = file.OpenReadStream();
        var result = await mediaService.UploadAsync(stream, file.FileName, file.Length, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var result = await mediaService.GetByIdAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await mediaService.DeleteAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    /// <summary>
    /// Toplu/yikici bir bakim islemi - PublishingController'daki gerekcenin
    /// ayni: Admin'e kilitli, Editor tetikleyemez.
    /// </summary>
    [HttpPost("cleanup-orphans")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> CleanupOrphans(CancellationToken cancellationToken)
    {
        var result = await mediaService.CleanupOrphansAsync(cancellationToken);
        return result.ToActionResult(this);
    }
}
