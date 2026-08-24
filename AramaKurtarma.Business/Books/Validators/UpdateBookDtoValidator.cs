using FluentValidation;

namespace AramaKurtarma.Business.Books;

public sealed class UpdateBookDtoValidator : AbstractValidator<UpdateBookDto>
{
    public UpdateBookDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(200)
            .Matches("^[a-z0-9]+(-[a-z0-9]+)*$")
            .WithMessage("Slug sadece kucuk harf, rakam ve tire icerebilir (orn. 'kentsel-arama-kurtarma').");

        RuleFor(x => x.Description)
            .MaximumLength(2000);

        RuleFor(x => x.LanguageCode)
            .NotEmpty()
            .MaximumLength(10);
    }
}
