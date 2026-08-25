using Isbak_SAR_Guide.API.Extensions;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Isbak_SAR_Guide.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(dto, cancellationToken);
        return result.ToActionResult(this);
    }
}
