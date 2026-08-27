using System.Text.Json;
using FluentValidation;
using Isbak_SAR_Guide.Business.DTOs.ContentBlocks;

namespace Isbak_SAR_Guide.Business.Validation.ContentBlocks;

public sealed class CreateContentBlockDtoValidator : AbstractValidator<CreateContentBlockDto>
{
    public CreateContentBlockDtoValidator()
    {
        // System.Text.Json enum'u sinir kontrolu yapmadan deserialize eder -
        // {"type": 99} model binding'den sessizce gecer, DB'deki CHECK
        // constraint'e kadar yakalanmaz. IsInEnum ile burada, erken yakalanir.
        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.Text)
            .MaximumLength(10000);

        RuleFor(x => x.DataJson)
            .Must(BeValidJsonOrNull)
            .WithMessage("DataJson gecerli bir JSON olmali.");
    }

    private static bool BeValidJsonOrNull(string? dataJson)
    {
        if (dataJson is null)
        {
            return true;
        }

        try
        {
            using var _ = JsonDocument.Parse(dataJson);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
