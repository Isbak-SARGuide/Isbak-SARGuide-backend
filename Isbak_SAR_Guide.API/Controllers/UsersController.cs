using System.Security.Claims;
using Asp.Versioning;
using Isbak_SAR_Guide.API.Common;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

/// <summary>
/// Kullanici yonetimi. Sinif seviyesinde KASITLI OLARAK sadece [Authorize]
/// (rol kisiti yok) - her eylem kendi rol gereksinimini ayrica bildirir.
/// Faz 13.6'da denenip 403 ile basarisiz olan ilk tasarim, sinif seviyesine
/// [Authorize(Roles = RoleNames.Admin)] koyup ChangeOwnPassword'e SADECE
/// [Authorize] eklemekti - beklenti "eylem seviyesi sinif seviyesini
/// gecersiz kilar" idi, ama ASP.NET Core coklu [Authorize] filtrelerini
/// BIRLESTIRIR (AND), en yakini kazanmaz: sonuc yine "authenticated VE Admin"
/// oldu, Editor icin 403. Canli HTTP testiyle (ChangeOwnPassword_WithEditorToken_ReturnsNoContent)
/// yakalandi. Dogru cozum: rol kisitini sinif yerine SADECE Admin-only
/// eylemlere (Create/GetAll/ChangeRole/Deactivate) tek tek koymak.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Create(CreateUserDto dto, CancellationToken cancellationToken)
    {
        var result = await userService.CreateAsync(dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> GetAll([FromQuery] int page, [FromQuery] int pageSize, CancellationToken cancellationToken)
    {
        var result = await userService.GetAllAsync(
            PagingDefaults.NormalizePage(page), PagingDefaults.NormalizePageSize(pageSize), cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPut("{id}/role")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> ChangeRole(string id, ChangeRoleDto dto, CancellationToken cancellationToken)
    {
        var result = await userService.ChangeRoleAsync(id, dto, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{id}/deactivate")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Deactivate(string id, CancellationToken cancellationToken)
    {
        var actingUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (actingUserId is null)
        {
            return Unauthorized();
        }

        var result = await userService.DeactivateAsync(id, actingUserId, cancellationToken);
        return result.ToActionResult(this);
    }

    [HttpPost("{id}/activate")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<IActionResult> Activate(string id, CancellationToken cancellationToken)
    {
        var result = await userService.ActivateAsync(id, cancellationToken);
        return result.ToActionResult(this);
    }

    // Sinif seviyesindeki [Authorize] disinda ek rol kisiti YOK, bilerek:
    // herhangi bir authenticated kullanici (Admin veya Editor) sadece KENDI
    // sifresini degistirebilir.
    [HttpPut("me/password")]
    public async Task<IActionResult> ChangeOwnPassword(ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
        {
            return Unauthorized();
        }

        var result = await userService.ChangeOwnPasswordAsync(userId, dto, cancellationToken);
        return result.ToActionResult(this);
    }
}
