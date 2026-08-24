using AramaKurtarma.Entities.Common;

namespace AramaKurtarma.Entities.Content;

public class Content : BaseEntity
{
    public int ModuleId { get; set; }

    public string Title { get; set; } = null!;

    public string? Summary { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    public Module Module { get; set; } = null!;

    public ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();
}
