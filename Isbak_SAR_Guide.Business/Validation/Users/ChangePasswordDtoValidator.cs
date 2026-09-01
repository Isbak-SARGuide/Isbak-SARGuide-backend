using FluentValidation;
using Isbak_SAR_Guide.Business.DTOs.Users;

namespace Isbak_SAR_Guide.Business.Validation.Users;

public sealed class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty();

        // Sifre karmasikligi burada TEKRARLANMAZ - bkz. CreateUserDtoValidator,
        // ayni gerekce: UserManager.ChangePasswordAsync IdentityOptions.Password
        // politikasina karsi zaten dogrular.
        RuleFor(x => x.NewPassword)
            .NotEmpty();
    }
}
