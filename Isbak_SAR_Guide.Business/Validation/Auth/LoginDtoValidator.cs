using FluentValidation;
using Isbak_SAR_Guide.Business.DTOs.Auth;

namespace Isbak_SAR_Guide.Business.Validation.Auth;

public sealed class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        // Burada SADECE "alan bos mu" kontrolu var. Sifre uzunlugu/karmasikligi
        // kurallari giriste DEGIL, kayit/sifre-degistirme akisinda uygulanir.
        // Girise kural koymak, mevcut sifresi eski politikaya uyan kullaniciyi
        // kilitler ve saldirgana politika hakkinda bilgi sizdirir.
        RuleFor(x => x.UserName)
            .NotEmpty().WithMessage("Kullanici adi zorunludur.")
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Sifre zorunludur.");
    }
}
