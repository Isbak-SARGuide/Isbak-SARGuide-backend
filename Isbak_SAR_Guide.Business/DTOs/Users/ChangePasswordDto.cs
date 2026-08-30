namespace Isbak_SAR_Guide.Business.DTOs.Users;

/// <summary>Herhangi bir authenticated kullanici kendi sifresi icin cagirir (bkz. UsersController.ChangeOwnPassword).</summary>
public sealed record ChangePasswordDto(string CurrentPassword, string NewPassword);
