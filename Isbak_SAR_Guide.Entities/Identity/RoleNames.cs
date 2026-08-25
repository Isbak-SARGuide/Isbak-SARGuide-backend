namespace Isbak_SAR_Guide.Entities.Identity;

/// <summary>
/// Rol adlari tek yerde: seed (DataAccess) ve [Authorize] attribute'lari (API)
/// ayni sabitten okur. Literal string iki yerde yazilip birinde yanlis
/// yazildiginda derleyicinin yakalayamayacagi sessiz bir yetki bug'i olurdu.
/// Entities'te cunku hicbir seye bagimli olmayan tek katman bu.
/// </summary>
public static class RoleNames
{
    public const string Admin = "Admin";

    public const string Editor = "Editor";
}
