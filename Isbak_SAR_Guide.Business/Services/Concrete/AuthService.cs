using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Auth;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

public class AuthService(
    UserManager<ApplicationUser> userManager,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IAuthService
{
    private static readonly Error _invalidCredentialsError =
        Error.Unauthorized("Auth.InvalidCredentials", "Kullanıcı adı veya şifre hatalı.");

    private static readonly Error _invalidRefreshTokenError =
        Error.Unauthorized("Auth.InvalidRefreshToken", "Geçersiz veya süresi dolmuş yenileme belirteci.");

    public async Task<Result<LoginResponseDto>> LoginAsync(LoginDto dto, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByNameAsync(dto.UserName);

        // "Kullanici yok", "sifre yanlis" VE "hesap kilitli" ayni jenerik
        // mesaji doner - ucunu de ayirmak enumeration/hesap-varligi sizintisi
        // olurdu (mevcut not-found/wrong-password ilkesiyle ayni, bkz. CLAUDE.md).
        if (user is null)
        {
            return Result.Failure<LoginResponseDto>(_invalidCredentialsError);
        }

        if (await userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<LoginResponseDto>(_invalidCredentialsError);
        }

        if (!await userManager.CheckPasswordAsync(user, dto.Password))
        {
            // Basarisiz denemeyi sayar; esik asilirsa Lockout.DefaultLockoutTimeSpan
            // kadar IsLockedOutAsync true doner (DataAccess/DependencyInjection.cs'teki
            // Lockout ayarlari).
            await userManager.AccessFailedAsync(user);
            return Result.Failure<LoginResponseDto>(_invalidCredentialsError);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var refreshToken = await IssueRefreshTokenAsync(user.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoginResponseDto(
            AccessToken: accessToken.Token,
            ExpiresAtUtc: accessToken.ExpiresAtUtc,
            UserName: user.UserName!,
            FullName: user.FullName,
            Roles: roles.ToList(),
            RefreshToken: refreshToken.Token));
    }

    public async Task<Result<LoginResponseDto>> RefreshAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(dto.RefreshToken);
        var existing = await unitOfWork.RefreshTokens.FindByTokenHashAsync(tokenHash, cancellationToken);

        if (existing is null)
        {
            return Result.Failure<LoginResponseDto>(_invalidRefreshTokenError);
        }

        if (existing.RevokedAtUtc is not null)
        {
            // Reuse tespiti: rotasyonla iptal edilmis bir token tekrar sunuldu -
            // calinmis olabilir. Kullanicinin TUM aktif token'larini hemen iptal et.
            await unitOfWork.RefreshTokens.RevokeAllActiveForUserAsync(existing.UserId, cancellationToken);
            return Result.Failure<LoginResponseDto>(_invalidRefreshTokenError);
        }

        if (existing.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return Result.Failure<LoginResponseDto>(_invalidRefreshTokenError);
        }

        var user = await userManager.FindByIdAsync(existing.UserId);
        if (user is null)
        {
            return Result.Failure<LoginResponseDto>(_invalidRefreshTokenError);
        }

        // Defense-in-depth: UserService.DeactivateAsync zaten bu kullanicinin
        // aktif token'larini iptal ediyor (RevokeAllActiveForUserAsync), ama bu
        // ikinci bir bariyer - LoginAsync'in aksine burasi eskiden IsLockedOutAsync
        // kontrol etmiyordu (kod inceleme bulgusu, roadmap 13.6).
        if (await userManager.IsLockedOutAsync(user))
        {
            return Result.Failure<LoginResponseDto>(_invalidRefreshTokenError);
        }

        // Rotasyon: eskisi iptal, yenisi uretilir - ikisi de ayni SaveChanges'te.
        existing.RevokedAtUtc = DateTime.UtcNow;

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = tokenService.GenerateAccessToken(user, roles);
        var newRefreshToken = await IssueRefreshTokenAsync(user.Id, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new LoginResponseDto(
            AccessToken: accessToken.Token,
            ExpiresAtUtc: accessToken.ExpiresAtUtc,
            UserName: user.UserName!,
            FullName: user.FullName,
            Roles: roles.ToList(),
            RefreshToken: newRefreshToken.Token));
    }

    public async Task<Result> RevokeAsync(RefreshTokenRequestDto dto, CancellationToken cancellationToken = default)
    {
        var tokenHash = tokenService.HashRefreshToken(dto.RefreshToken);
        var existing = await unitOfWork.RefreshTokens.FindByTokenHashAsync(tokenHash, cancellationToken);

        // Idempotent: token yoksa ya da zaten iptalse de basarili doner -
        // logout'un iki kez tetiklenmesi (cift tikla, sekme kapat vb.) hata degil.
        if (existing is not null && existing.RevokedAtUtc is null)
        {
            existing.RevokedAtUtc = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private async Task<RefreshTokenResult> IssueRefreshTokenAsync(string userId, CancellationToken cancellationToken)
    {
        var generated = tokenService.GenerateRefreshToken();

        var entity = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenService.HashRefreshToken(generated.Token),
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = generated.ExpiresAtUtc,
        };

        await unitOfWork.RefreshTokens.AddAsync(entity, cancellationToken);
        return generated;
    }
}
