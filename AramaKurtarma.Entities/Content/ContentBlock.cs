using AramaKurtarma.Entities.Common;
using AramaKurtarma.Entities.Content.Enums;

namespace AramaKurtarma.Entities.Content;

public class ContentBlock : BaseEntity
{
    public int ContentId { get; set; }

    public ContentBlockType Type { get; set; }

    public string? Text { get; set; }

    /// <summary>
    /// Table, Warning, Animation gibi yapisal blok verisi (jsonb).
    /// </summary>
    public string? DataJson { get; set; }

    public int? MediaId { get; set; }

    public int DisplayOrder { get; set; }

    public Content Content { get; set; } = null!;

    public Media? Media { get; set; }
}
