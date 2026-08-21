using AramaKurtarma.Entities.Content.Enums;

namespace AramaKurtarma.Entities.Content;

public class ContentBlock
{
    public int Id { get; set; }

    public int ContentId { get; set; }

    public ContentBlockType Type { get; set; }

    public string? Text { get; set; }

    public int? MediaId { get; set; }

    public int DisplayOrder { get; set; }

    public Content Content { get; set; } = null!;

    public Media? Media { get; set; }
}