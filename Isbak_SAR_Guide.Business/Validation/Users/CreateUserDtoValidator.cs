using FluentValidation;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Entities.Identity;

namespace Isbak_SAR_Guide.Business.Validation.Users;

public sealed class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.UserName)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.FullName)
            .NotEmpty()
            .MaximumLength(200);

        // Sifre karmasikligi (uzunluk, rakam, buyuk harf vb.) burada
        // TEKRARLANMAZ - UserManager.CreateAsync zaten IdentityOptions.Password
        // politikasina karsi dogrular, IdentityResult hatalari UserService'te
        // Error.Validation'a cevrilir. Burasi sadece bos gecmesin diye erken kontrol.
        RuleFor(x => x.Password)
            .NotEmpty();

        RuleFor(x => x.Role)
            .Must(role => role is RoleNames.Admin or RoleNames.Editor)
            .WithMessage($"Rol '{RoleNames.Admin}' veya '{RoleNames.Editor}' olmalı.");
    }
}
