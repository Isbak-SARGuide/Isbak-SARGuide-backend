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

    public ApplicationUser User { get; set; } = null!;
}
