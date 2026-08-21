using AramaKurtarma.Entities.Content.Enums;

namespace AramaKurtarma.Entities.Content;

public class Media
{
    public int Id { get; set; }

    public string FileName { get; set; } = null!;

    public string StoragePath { get; set; } = null!;

    public MediaType MediaType { get; set; }

    public string ContentType { get; set; } = null!;

    public long FileSize { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public double? Duration { get; set; }

    public DateTime CreatedAt { get; set; }
}