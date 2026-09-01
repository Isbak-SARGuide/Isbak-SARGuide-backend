using FluentValidation;
using Isbak_SAR_Guide.Business.DTOs.Users;
using Isbak_SAR_Guide.Entities.Identity;

namespace Isbak_SAR_Guide.Business.Validation.Users;

public sealed class ChangeRoleDtoValidator : AbstractValidator<ChangeRoleDto>
{
    public ChangeRoleDtoValidator()
    {
        RuleFor(x => x.Role)
            .Must(role => role is RoleNames.Admin or RoleNames.Editor)
            .WithMessage($"Rol '{RoleNames.Admin}' veya '{RoleNames.Editor}' olmalı.");
    }
}
