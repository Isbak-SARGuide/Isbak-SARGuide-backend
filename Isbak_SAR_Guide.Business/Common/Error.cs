namespace Isbak_SAR_Guide.Business.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden,
    Unexpected,
}

/// <summary>
/// Controller katmani bu Type'a bakarak HTTP durum koduna cevirir:
/// Validation->400, Unauthorized->401, Forbidden->403, NotFound->404,
/// Conflict->409, Unexpected->500.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    /// <summary>Kimlik dogrulanamadi - kim oldugunu kanitlayamadin (401).</summary>
    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    /// <summary>Kimlik dogrulandi ama yetki yok - kim oldugunu biliyoruz, izin yok (403).</summary>
    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
