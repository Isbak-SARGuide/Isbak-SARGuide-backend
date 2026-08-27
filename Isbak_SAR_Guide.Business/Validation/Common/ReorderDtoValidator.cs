using FluentValidation;
using Isbak_SAR_Guide.Business.DTOs.Common;

namespace Isbak_SAR_Guide.Business.Validation.Common;

public sealed class ReorderDtoValidator : AbstractValidator<ReorderDto>
{
    public ReorderDtoValidator()
    {
        RuleFor(x => x.OrderedIds)
            .NotEmpty();

        // Tekrarlanan bir id, o kaydin nihai DisplayOrder'inin belirsiz olmasi
        // demek - servis katmanindaki sibling-set esitligi bunu zaten yakalar
        // ama burada erken ve acik bir mesajla reddetmek daha iyi bir hata verir.
        RuleFor(x => x.OrderedIds)
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("OrderedIds icinde tekrarlanan id olamaz.")
            .When(x => x.OrderedIds.Count > 0);
    }
}
