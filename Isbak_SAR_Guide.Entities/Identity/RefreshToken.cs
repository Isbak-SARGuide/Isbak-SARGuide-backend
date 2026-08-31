namespace Isbak_SAR_Guide.Entities.Identity;

/// <summary>
/// BaseEntity'den bilerek turemez: iptal (RevokedAtUtc) soft-delete degil,
/// acik bir alan - BookPublication/PublishedContent'teki "bilerek IRepository&lt;T&gt;
/// disi" gerekcesiyle ayni (CLAUDE.md).
/// </summary>
public class RefreshToken
{
    public int Id { get; set; }

    public string UserId { get; set; } = null!;

    /// <summary>Ham token asla saklanmaz - sadece SHA-256 ozeti (sifre gibi).</summary>
    public string TokenHash { get; set; } = null!;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    /// <summary>Null = hala aktif. Rotasyonda veya acik logout'ta doldurulur.</summary>
    public DateTime? RevokedAtUtc { get; set; }

    /// <summary>
    /// RevokedAtUtc'nin NEDENini ayirt eder - sadece rotasyon (RefreshAsync'in
    /// kendi tek-kullanimlik rotasyonu) bunu true yapar; acik logout
    /// (RevokeAsync) ve toplu iptal (RevokeAllActiveForUserAsync - reuse
    /// tespiti/deaktivasyon) false birakir (varsayilan). AuthService.RefreshAsync'in
    /// rotasyon grace window'u (roadmap doc §13.10) SADECE bu true iken devreye
    /// girer - aksi halde acik bir logout'tan hemen sonra ayni token'la tekrar
    /// giris yapilabilir gibi bir guvenlik acigi olurdu.
    /// </summary>
    public bool RevokedByRotation { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
