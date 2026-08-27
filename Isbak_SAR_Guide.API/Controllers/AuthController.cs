using Asp.Versioning;
using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Isbak_SAR_Guide.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(dto, cancellationToken);
        return result.ToActionResult(this);
    }

    // Refresh de ayni limite tabi: bir saldirganin refresh token'i bruteforce
    // etmeye calismasi da login denemesi kadar kisitli olmali.
    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Refresh(RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(dto, cancellationToken);
        return result.ToActionResult(this);
    }

    // AllowAnonymous: elindeki (gecerli veya suresi gecmis) refresh token'in
    // kendisi zaten yetkinin kaniti - access token'in ayrica gecerli olmasini
    // sart kosmak, access token'i suresi dolmus bir istemcinin logout bile
    // yapamamasina yol acardi.
    [HttpPost("revoke")]
    [AllowAnonymous]
    public async Task<IActionResult> Revoke(RefreshTokenRequestDto dto, CancellationToken cancellationToken)
    {
        var result = await authService.RevokeAsync(dto, cancellationToken);
        return result.ToActionResult(this);
    }
}
