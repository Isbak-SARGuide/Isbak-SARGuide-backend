using AramaKurtarma.Entities.Common;

namespace AramaKurtarma.Entities.Content;

public class Book : BaseEntity
{
    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public string LanguageCode { get; set; } = "tr";

    public int Version { get; set; }

    public bool IsPublished { get; set; }

    public ICollection<Module> Modules { get; set; } = new List<Module>();
}
