using System.Security.Claims;
using Asp.Versioning;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.DTOs.Publishing;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

/// <summary>
/// Yayin eylemleri. Route REST'ci (kaynak + eylem), sinif ayri: BooksController
/// IBookService'iyle, burasi IPublishingService'iyle yalin kalir; rollback /
/// yayin gecmisi gibi gelecek uclarin dogal evi burasi.
///
/// Rol sinif seviyesinde: bu controller'a eklenecek her eylem admin isi.
/// Editor icerik duzenler ama yayinlayamaz - yayin, mobil sahaya giden veriyi
/// degistirir; onay yetkisi bilincli olarak Admin'e kilitlidir.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/books/{bookId:int}/publish")]
[Authorize(Roles = RoleNames.Admin)]
public class PublishingController(IPublishingService publishingService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Publish(int bookId, CancellationToken cancellationToken)
    {
        // TokenService "sub" claim'i yazar; ASP.NET inbound claim mapping
        // (varsayilan acik) onu NameIdentifier'a cevirir - bilerek, sans degil.
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        // Fallback policy kimliksizi iceri sokmaz - bu guard'in korudugu
        // senaryo farkli: imzali ama id claim'i tasimayan token (yanlis
        // konfigurasyon, baska servis icin uretilmis token). Sunucu hatasini
        // 500 log kirliligi yerine 401 ile durustce doner.
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await publishingService.PublishAsync(bookId, userId, cancellationToken);

        // Basari 200 OK + PublishResultDto: Location verilecek bir
        // GET /publications/{id} ucumuz yok - olmayan adrese isaret eden 201,
        // yalan soyleyen 201 olurdu. Yayin gecmisi endpoint'i gelirse evrilir.
        return result.ToActionResult(this);
    }

    // Mutlak route: sinif seviyesindeki template "/publish" ile bitiyor,
    // rollback onun ALTINDA degil YANINDA bir kaynak - "/" ile baslayan
    // mutlak override, sinif template'ini bu eylem icin gecersiz kilar.
    [HttpPost("/api/v{version:apiVersion}/books/{bookId:int}/rollback")]
    public async Task<IActionResult> Rollback(int bookId, RollbackRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await publishingService.RollbackAsync(bookId, dto.ToVersion, userId, cancellationToken);

        return result.ToActionResult(this);
    }
}
