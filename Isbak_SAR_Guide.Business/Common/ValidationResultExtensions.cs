using FluentValidation.Results;

namespace Isbak_SAR_Guide.Business.Common;

/// <summary>
/// Book/Module/Content/ContentBlock/User servislerinin hepsinde birebir
/// tekrarlanan "hata mesajlarini birlestir + Error.Validation'a cevir" kalibi
/// (Faz 8 mimari incelemesinde bulundu, 12 tekrar) - tek yere toplandi.
/// </summary>
public static class ValidationResultExtensions
{
    public static Error ToValidationError(this ValidationResult validationResult, string code)
    {
        var message = string.Join("; ", validationResult.Errors.Select(e => e.ErrorMessage));
        return Error.Validation(code, message);
    }
}
