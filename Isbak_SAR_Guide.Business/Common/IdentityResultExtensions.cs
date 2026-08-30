using Microsoft.AspNetCore.Identity;

namespace Isbak_SAR_Guide.Business.Common;

/// <summary>
/// UserService'te tekrarlanan "IdentityResult hatalarini birlestir + Error.Validation'a
/// cevir" kalibi (Faz 13.6 code review'unda bulundu, ayni ValidationResultExtensions
/// gerekcesi ama FluentValidation degil IdentityResult icin) - tek yere toplandi.
/// </summary>
public static class IdentityResultExtensions
{
    public static Error ToValidationError(this IdentityResult result, string code)
    {
        var message = string.Join("; ", result.Errors.Select(e => e.Description));
        return Error.Validation(code, message);
    }
}
