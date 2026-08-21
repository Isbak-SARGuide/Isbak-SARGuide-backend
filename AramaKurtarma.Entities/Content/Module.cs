namespace AramaKurtarma.Entities.Content;

public class Module
{
    public int Id { get; set; }

    public int BookId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsPublished { get; set; }

    public Book Book { get; set; } = null!;

    public ICollection<Content> Contents { get; set; } = new List<Content>();
}