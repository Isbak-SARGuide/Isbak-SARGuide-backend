using Asp.Versioning;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

/// <summary>
/// Kullanici olusturma - Admin-only (roadmap 9.2). Kayit (self sign-up) yok:
/// bir Editor hesabinin var olmasinin tek yolu bir Admin'in burasi uzerinden
/// acmasi.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/users")]
[Authorize(Roles = RoleNames.Admin)]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateUserDto dto, CancellationToken cancellationToken)
    {
        var result = await userService.CreateAsync(dto, cancellationToken);
        return result.ToActionResult(this);
    }
}
