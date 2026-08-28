namespace Isbak_SAR_Guide.Business.DTOs.Users;

public sealed record UserDto(
    string Id,
    string UserName,
    string FullName,
    IReadOnlyList<string> Roles);
