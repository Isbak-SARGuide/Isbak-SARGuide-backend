using AramaKurtarma.Business.Common;
using AramaKurtarma.Business.DTOs.Auth;
using AramaKurtarma.Business.Services.Abstract;
using AramaKurtarma.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace AramaKurtarma.Business.Services.Concrete;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService) : IAuthService
{
    public async Task<Result<LoginResponseDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(dto.UserName);

        if (user is null || !await userManager.CheckPasswordAsync(user, dto.Password))
        {
            return Result.Failure<LoginResponseDto>(
                Error.Unauthorized("Auth.InvalidCredentials", "Kullanıcı adı veya şifre hatalı."));
        }
        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        return Result.Success(new LoginResponseDto(
            accessToken.Token,
            accessToken.ExpiresAtUtc,
            user.UserName!,
            user.FullName,
            roles.ToList()));  


    }

}
