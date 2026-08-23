namespace KatalogCzesci.Models;

public class CatalogResponse
{
    public Data? Data { get; set; }
}

public class Data
{
    public MyAds? MyAds { get; set; }
}

public class MyAds
{
    public Ads? Ads { get; set; }
}

public class Ads
{
    public int TotalCount { get; set; }

    public List<Ad> Items { get; set; } = [];
}