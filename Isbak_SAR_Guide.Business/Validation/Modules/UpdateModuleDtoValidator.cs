using FluentValidation;
using Isbak_SAR_Guide.Business.DTOs.Modules;

namespace Isbak_SAR_Guide.Business.Validation.Modules;

public sealed class UpdateModuleDtoValidator : AbstractValidator<UpdateModuleDto>
{
    public UpdateModuleDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Description)
            .MaximumLength(2000);
    }
}
