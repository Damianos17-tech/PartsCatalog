namespace KatalogCzesci.Models;

public class Ad
{
    public long Id { get; set; }

    public string Title { get; set; } = "";

    public string Price { get; set; } = "";

    public List<string> Photos { get; set; } = [];

    public List<string> Categories { get; set; } = [];

    public Location? Location { get; set; }

    public Stats? Stats { get; set; }
}

public class Location
{
    public string Name { get; set; } = "";
}

public class Stats
{
    public int Views { get; set; }

    public int Observed { get; set; }

    public int Phones { get; set; }
}