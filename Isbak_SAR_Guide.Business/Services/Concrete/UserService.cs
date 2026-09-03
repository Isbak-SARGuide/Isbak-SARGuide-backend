using FluentValidation;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.DataAccess.Repositories.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

/// <summary>
/// Kayit (self sign-up) yok, bilerek: yeni hesap sadece bir Admin'in
/// UsersController uzerinden acmasiyla var olur (roadmap 9.2).
/// </summary>
public class UserService(
    UserManager<ApplicationUser> userManager,
    IUnitOfWork unitOfWork,
    IValidator<CreateUserDto> createValidator,
    IValidator<ChangeRoleDto> changeRoleValidator,
    IValidator<ChangePasswordDto> changePasswordValidator) : IUserService
{
    public async Task<Result<UserDto>> CreateAsync(CreateUserDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await createValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<UserDto>(validationResult.ToValidationError("User.ValidationFailed"));
        }

        if (await userManager.FindByNameAsync(dto.UserName) is not null)
        {
            return Result.Failure<UserDto>(Error.Conflict("User.AlreadyExists", $"'{dto.UserName}' kullanıcı adı zaten kullanılıyor."));
        }

        var user = new ApplicationUser
        {
            UserName = dto.UserName,
            Email = $"{dto.UserName}@isbak-sar-guide.local",
            EmailConfirmed = true,
            FullName = dto.FullName,
        };

        var createResult = await userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            return Result.Failure<UserDto>(createResult.ToValidationError("User.CreateFailed"));
        }

        var roleResult = await userManager.AddToRoleAsync(user, dto.Role);
        if (!roleResult.Succeeded)
        {
            // Yari-olusmus (rolsuz) bir hesap birakmak yerine geri al - aksi
            // halde Admin "kullanici olustu" sanip rolsuz bir hesabin var
            // olduğunu asla ogrenmezdi (Faz 8 mimari incelemesinde bulundu).
            await userManager.DeleteAsync(user);
            return Result.Failure<UserDto>(roleResult.ToValidationError("User.RoleAssignmentFailed"));
        }

        return Result.Success(new UserDto(user.Id, user.UserName!, user.FullName, [dto.Role]));
    }

    public async Task<Result<PagedResult<UserDto>>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = await userManager.Users.CountAsync(cancellationToken);

        var users = await userManager.Users
            .OrderBy(u => u.UserName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        // Kullanici basina GetRolesAsync/IsLockedOutAsync (N+1) - admin panelin
        // kullanici listesi Module/Content gibi yuzlerce satir buyumez (bu
        // kadar sinirli olcekte ekstra bir tek-sorgu projeksiyonu ModuleWithContentCount
        // gibi ayrica bir tip gerektirmez, YAGNI).
        var items = new List<UserDto>(users.Count);
        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            var isLockedOut = await userManager.IsLockedOutAsync(user);
            items.Add(new UserDto(user.Id, user.UserName!, user.FullName, roles.ToList(), isLockedOut));
        }

        return Result.Success(new PagedResult<UserDto>(items, totalCount, page, pageSize));
    }

    public async Task<Result<UserDto>> ChangeRoleAsync(string id, ChangeRoleDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await changeRoleValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure<UserDto>(validationResult.ToValidationError("User.ValidationFailed"));
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Result.Failure<UserDto>(Error.NotFound("User.NotFound", $"Id={id} olan kullanıcı bulunamadı."));
        }

        var currentRoles = await userManager.GetRolesAsync(user);

        // Sistemdeki SON Admin'i Editor'a dusuremezsin - kurtarma yolu olmayan
        // bir kilitlenme senaryosu, DeleteAsync'teki Admin-silinemez guard'la
        // ayni gerekce (kod inceleme bulgusu, roadmap 13.6).
        if (currentRoles.Contains(RoleNames.Admin) && dto.Role != RoleNames.Admin)
        {
            var adminCount = (await userManager.GetUsersInRoleAsync(RoleNames.Admin)).Count;
            if (adminCount <= 1)
            {
                return Result.Failure<UserDto>(Error.Validation("User.LastAdminProtected", "Sistemde en az bir Admin kalmalı, bu kullanıcının rolü değiştirilemez."));
            }
        }

        // Once YENI rolu ekle, SONRA eskilerini kaldir (tersi degil) - UserManager
        // her cagriyi ayri/aninda commit eder (tek bir transaction degil), bu
        // yuzden ikinci adim basarisiz olursa kullanici rolsuz degil FAZLA rollu
        // kalir (daha az kotu bir ara durum). Zaten dto.Role'e sahipse AddToRoleAsync
        // "already in role" hatasi verir, o yuzden onceden kontrol edilir.
        if (!currentRoles.Contains(dto.Role))
        {
            var addResult = await userManager.AddToRoleAsync(user, dto.Role);
            if (!addResult.Succeeded)
            {
                return Result.Failure<UserDto>(addResult.ToValidationError("User.RoleAssignmentFailed"));
            }
        }

        var rolesToRemove = currentRoles.Where(r => r != dto.Role).ToList();
        if (rolesToRemove.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                return Result.Failure<UserDto>(removeResult.ToValidationError("User.RoleAssignmentFailed"));
            }
        }

        return Result.Success(new UserDto(user.Id, user.UserName!, user.FullName, [dto.Role]));
    }

    public async Task<Result> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", $"Id={id} olan kullanıcı bulunamadı."));
        }

        // Admin hesaplari hic silinemez (urun karari) - ayrica
        // BookPublication.PublishedById (Restrict) sadece Admin'lere isaret
        // edebilir (PublishingController Admin-only), bu yuzden bu kontrol
        // ayni zamanda o FK'yi hic tetiklenmeyecek sekilde bastan engeller.
        if (await userManager.IsInRoleAsync(user, RoleNames.Admin))
        {
            return Result.Failure(Error.Validation("User.AdminDeletionForbidden", "Admin hesapları silinemez."));
        }

        // RefreshToken.UserId Cascade oldugu icin (RefreshTokenConfiguration)
        // ayrica bir RevokeAllActiveForUserAsync cagrisina gerek yok - satirlar
        // kullaniciyla birlikte otomatik silinir.
        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return Result.Failure(result.ToValidationError("User.DeletionFailed"));
        }

        return Result.Success();
    }

    public async Task<Result> ChangeOwnPasswordAsync(string userId, ChangePasswordDto dto, CancellationToken cancellationToken = default)
    {
        var validationResult = await changePasswordValidator.ValidateAsync(dto, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Result.Failure(validationResult.ToValidationError("User.ValidationFailed"));
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", $"Id={userId} olan kullanıcı bulunamadı."));
        }

        var result = await userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
        {
            return Result.Failure(result.ToValidationError("User.PasswordChangeFailed"));
        }

        // Sifre calinmis olabilecegi icin degistiriliyor olabilir - eski
        // oturumlarin (refresh token'larin) hayatta kalmasi bu senaryoda
        // korumayi bosa cikarirdi, ayni DeactivateAsync'teki gerekce.
        await unitOfWork.RefreshTokens.RevokeAllActiveForUserAsync(userId, cancellationToken);

        return Result.Success();
    }
}
