namespace AramaKurtarma.Business.Common;

public enum ErrorType
{
    Validation,
    NotFound,
    Conflict,
    Unexpected,
}

/// <summary>
/// Controller katmani bu Type'a bakarak HTTP durum koduna cevirir:
/// Validation->400, NotFound->404, Conflict->409, Unexpected->500.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error Unexpected(string code, string message) => new(code, message, ErrorType.Unexpected);
}
