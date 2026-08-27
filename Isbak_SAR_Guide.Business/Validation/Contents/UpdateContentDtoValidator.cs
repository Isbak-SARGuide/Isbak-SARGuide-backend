using FluentValidation;
using Isbak_SAR_Guide.Business.DTOs.Contents;

namespace Isbak_SAR_Guide.Business.Validation.Contents;

public sealed class UpdateContentDtoValidator : AbstractValidator<UpdateContentDto>
{
    public UpdateContentDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Summary)
            .MaximumLength(500);

        RuleFor(x => x.VariantGroupKey)
            .MaximumLength(100);

        RuleFor(x => x.VariantLabel)
            .MaximumLength(50);

        RuleFor(x => x)
            .Must(x => (x.VariantGroupKey is null) == (x.VariantLabel is null))
            .WithMessage("VariantGroupKey ve VariantLabel birlikte doldurulmali ya da ikisi de bos birakilmali.");
    }
}
