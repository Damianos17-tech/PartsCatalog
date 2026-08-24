namespace KatalogCzesci.Models;

public class Ad
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public long? OlxId { get; set; }

    public string Title { get; set; } = "";

    public string Description { get; set; } = "";

    public decimal Price { get; set; }

    public List<string> Photos { get; set; } = [];

    public List<string> Categories { get; set; } = [];

    public DateTime? ActivatedAt { get; set; }

    public AdStatus Status { get; set; } = AdStatus.Active;
}

public enum AdStatus
{
    Active,
    Sold
}