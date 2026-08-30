namespace Isbak_SAR_Guide.Business.DTOs.Users;

public sealed record UserDto(
    string Id,
    string UserName,
    string FullName,
    IReadOnlyList<string> Roles,
    // Faz 13.6, additive: sadece GetAllAsync doldurur - CreateAsync/
    // ChangeRoleAsync'te hep false kalir, tek bir kullaniciyi kilit-durumu
    // icin ayrica sorgulamaya gerek yok o yollarda.
    bool IsLockedOut = false);
