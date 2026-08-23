using System.Text.Json;
using KatalogCzesci.Models;

namespace KatalogCzesci.Services;

public class CatalogService
{
    private readonly IWebHostEnvironment _environment;

    public CatalogService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public async Task<List<Ad>> GetAdsAsync()
    {
        var filePath = Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "katalog2.json");

        if (!File.Exists(filePath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(filePath);

        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var response = JsonSerializer.Deserialize<CatalogResponse>(
            json,
            options);

        return response?.Data?.MyAds?.Ads?.Items ?? [];
    }
}