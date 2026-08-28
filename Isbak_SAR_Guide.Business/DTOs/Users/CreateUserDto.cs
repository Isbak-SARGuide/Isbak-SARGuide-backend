namespace Isbak_SAR_Guide.Business.DTOs.Users;

/// <summary>Sadece Admin cagirir (bkz. UsersController) - kayit acik degil, bilerek.</summary>
public sealed record CreateUserDto(
    string UserName,
    string Password,
    string FullName,
    string Role);
