using FluentValidation;
using Isbak_SAR_Guide.Business.Common;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Business.Services.Abstract;
using Isbak_SAR_Guide.Entities.Identity;
using Microsoft.AspNetCore.Identity;

namespace Isbak_SAR_Guide.Business.Services.Concrete;

/// <summary>
/// Kayit (self sign-up) yok, bilerek: yeni hesap sadece bir Admin'in
/// UsersController uzerinden acmasiyla var olur (roadmap 9.2).
/// </summary>
public class UserService(
    UserManager<ApplicationUser> userManager,
    IValidator<CreateUserDto> createValidator) : IUserService
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
}
