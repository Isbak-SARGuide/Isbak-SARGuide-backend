using Isbak_SAR_Guide.Entities.Common;
using Isbak_SAR_Guide.Entities.Content.Enums;

namespace Isbak_SAR_Guide.Entities.Content;

public class Media : BaseEntity
{
    public string FileName { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    /// <summary>
    /// Faz 12.7 (WebP + thumbnail, mobil optimizasyon): kucuk bir onizleme
    /// dosyasinin goreli yolu - StoragePath ile ayni "servable URL, aynen"
    /// kurali gecerli. Null = bu Media'nin thumbnail'i yok (Faz 12.7 ONCESI
    /// yuklenmis eski medya - geriye donuk backfill YAPILMADI, sadece yeni
    /// yuklemeler thumbnail uretir).
    /// </summary>
    public string? ThumbnailStoragePath { get; set; }

    public MediaType MediaType { get; set; }

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }

    /// <summary>
    /// SHA-256 checksum. Offline mobil istemci indirdigi dosyayi bununla dogrular.
    /// </summary>
    public string Checksum { get; set; } = null!;

    public int? Width { get; set; }

    public int? Height { get; set; }

    public double? Duration { get; set; }
}
