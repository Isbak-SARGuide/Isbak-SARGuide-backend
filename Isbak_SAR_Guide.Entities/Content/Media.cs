using Isbak_SAR_Guide.Entities.Common;
using Isbak_SAR_Guide.Entities.Content.Enums;

namespace Isbak_SAR_Guide.Entities.Content;

public class Media : BaseEntity
{
    public string FileName { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

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
