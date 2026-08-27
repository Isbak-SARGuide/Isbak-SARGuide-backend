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
            var message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
            return Result.Failure<UserDto>(Error.Validation("User.ValidationFailed", message));
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

        await userManager.AddToRoleAsync(user, dto.Role);

        return Result.Success(new UserDto(user.Id, user.UserName!, user.FullName, [dto.Role]));
    }
}
