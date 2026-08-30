using FluentValidation;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Business.Services.Abstract;
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
            var message = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return Result.Failure<UserDto>(Error.Validation("User.CreateFailed", message));
        }

        var roleResult = await userManager.AddToRoleAsync(user, dto.Role);
        if (!roleResult.Succeeded)
        {
            // Yari-olusmus (rolsuz) bir hesap birakmak yerine geri al - aksi
            // halde Admin "kullanici olustu" sanip rolsuz bir hesabin var
            // olduğunu asla ogrenmezdi (Faz 8 mimari incelemesinde bulundu).
            await userManager.DeleteAsync(user);
            var message = string.Join("; ", roleResult.Errors.Select(e => e.Description));
            return Result.Failure<UserDto>(Error.Validation("User.RoleAssignmentFailed", message));
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
        var removeResult = await userManager.RemoveFromRolesAsync(user, currentRoles);
        if (!removeResult.Succeeded)
        {
            var message = string.Join("; ", removeResult.Errors.Select(e => e.Description));
            return Result.Failure<UserDto>(Error.Validation("User.RoleAssignmentFailed", message));
        }

        var addResult = await userManager.AddToRoleAsync(user, dto.Role);
        if (!addResult.Succeeded)
        {
            var message = string.Join("; ", addResult.Errors.Select(e => e.Description));
            return Result.Failure<UserDto>(Error.Validation("User.RoleAssignmentFailed", message));
        }

        return Result.Success(new UserDto(user.Id, user.UserName!, user.FullName, [dto.Role]));
    }

    public async Task<Result> DeactivateAsync(string id, string actingUserId, CancellationToken cancellationToken = default)
    {
        // Kendi hesabini kilitleyen bir Admin, admin panele bir daha giremezdi -
        // kurtarma yolu olmayan bir kilitlenme senaryosu (roadmap 13.6).
        if (id == actingUserId)
        {
            return Result.Failure(Error.Validation("User.SelfDeactivationForbidden", "Kendi hesabınızı pasifleştiremezsiniz."));
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return Result.Failure(Error.NotFound("User.NotFound", $"Id={id} olan kullanıcı bulunamadı."));
        }

        var result = await userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
        if (!result.Succeeded)
        {
            var message = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure(Error.Validation("User.DeactivationFailed", message));
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
            var message = string.Join("; ", result.Errors.Select(e => e.Description));
            return Result.Failure(Error.Validation("User.PasswordChangeFailed", message));
        }

        return Result.Success();
    }
}
