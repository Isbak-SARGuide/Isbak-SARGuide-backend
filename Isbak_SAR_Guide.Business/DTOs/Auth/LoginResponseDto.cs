namespace Isbak_SAR_Guide.Business.DTOs.Auth;

/// <summary>
/// Frontend'in girisden sonra ihtiyac duydugu her sey. Sifre veya hash
/// ASLA burada yer almaz.
/// </summary>
public sealed record LoginResponseDto(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string UserName,
    string FullName,
    IReadOnlyList<string> Roles);
