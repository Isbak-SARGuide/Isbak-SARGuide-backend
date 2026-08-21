namespace AramaKurtarma.Entities.Content;

public class Content
{
    public int Id { get; set; }

    public int ModuleId { get; set; }

    public string Title { get; set; } = null!;

    public string? Summary { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public Module Module { get; set; } = null!;

    public ICollection<ContentBlock> Blocks { get; set; } = new List<ContentBlock>();
}