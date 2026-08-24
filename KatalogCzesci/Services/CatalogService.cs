
using System.Text.Json;
using KatalogCzesci.Models;

namespace KatalogCzesci.Services;

public class CatalogService
{
    private readonly IWebHostEnvironment _environment;

    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public CatalogService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }


    // --------------------------------------------------
    // ŚCIEŻKI
    // --------------------------------------------------

    private string SourceCatalogPath =>
        Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "katalog.json");

    private string LocalCatalogPath =>
        Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "catalog.local.json");


    // --------------------------------------------------
    // PUBLICZNE API
    // --------------------------------------------------

    public async Task<List<Ad>> GetAdsAsync()
    {
        // 1. Dodajemy tylko nowe ogłoszenia z katalog.json.
        await ImportNewAdsAsync();

        // 2. Uzupełniamy tylko brakujące dane.
        await FillMissingDataAsync();

        // 3. Zwracamy nasz lokalny katalog.
        return await LoadLocalCatalogAsync();
    }


    // --------------------------------------------------
    // IMPORT NOWYCH OGŁOSZEŃ
    // --------------------------------------------------

    public async Task<int> ImportNewAdsAsync()
    {
        var sourceAds = await LoadSourceAdsAsync();
        var localAds = await LoadLocalCatalogAsync();

        // Zbiór ID OLX, które już mamy lokalnie.
        var existingOlxIds = localAds
            .Where(ad => ad.OlxId.HasValue)
            .Select(ad => ad.OlxId!.Value)
            .ToHashSet();

        var addedCount = 0;

        foreach (var sourceAd in sourceAds)
        {
            // Jeżeli ogłoszenie już istnieje lokalnie,
            // niczego nie nadpisujemy.
            if (existingOlxIds.Contains(sourceAd.Id))
            {
                continue;
            }

            // Nowe ogłoszenie z katalog.json.
            var localAd = new Ad
            {
                // Nasze własne ID.
                Id = Guid.NewGuid(),

                // Oryginalne ID OLX.
                OlxId = sourceAd.Id,

                Title = sourceAd.Title ?? "",

                // Na razie opis pozostaje pusty.
                Description = "",

                Price = ParsePrice(sourceAd.Price),

                Photos = sourceAd.Photos ?? [],

                Categories = sourceAd.Categories ?? [],

                ActivatedAt = sourceAd.ActivatedAt,

                Status = AdStatus.Active
            };

            localAds.Add(localAd);

            existingOlxIds.Add(sourceAd.Id);

            addedCount++;
        }

        if (addedCount > 0)
        {
            await SaveLocalCatalogAsync(localAds);
        }

        return addedCount;
    }


    // --------------------------------------------------
    // UZUPEŁNIANIE BRAKUJĄCYCH DANYCH
    // --------------------------------------------------

    public async Task<int> FillMissingDataAsync()
    {
        var sourceAds = await LoadSourceAdsAsync();
        var localAds = await LoadLocalCatalogAsync();

        // Tworzymy szybki słownik:
        // OLX ID -> dane z katalog.json
        var sourceByOlxId = sourceAds
            .ToDictionary(ad => ad.Id);

        var updatedCount = 0;

        foreach (var localAd in localAds)
        {
            // Ręczne ogłoszenie nie ma OlxId.
            if (!localAd.OlxId.HasValue)
            {
                continue;
            }

            // Szukamy odpowiadającego ogłoszenia w katalog.json.
            if (!sourceByOlxId.TryGetValue(
                    localAd.OlxId.Value,
                    out var sourceAd))
            {
                continue;
            }

            // Uzupełniamy kategorię TYLKO jeśli
            // lokalny rekord jej jeszcze nie posiada.
            if (localAd.Categories.Count == 0 &&
                sourceAd.Categories?.Count > 0)
            {
                localAd.Categories = sourceAd.Categories;

                updatedCount++;
            }
        }

        if (updatedCount > 0)
        {
            await SaveLocalCatalogAsync(localAds);
        }

        return updatedCount;
    }


    // --------------------------------------------------
    // ZMIANA STATUSU OGŁOSZENIA
    // --------------------------------------------------

    public async Task<bool> SetStatusAsync(
        Guid adId,
        AdStatus status)
    {
        var localAds = await LoadLocalCatalogAsync();

        var ad = localAds.FirstOrDefault(
            x => x.Id == adId);

        if (ad == null)
        {
            return false;
        }

        ad.Status = status;

        await SaveLocalCatalogAsync(localAds);

        return true;
    }




    public async Task<bool> UpdateAdAsync(Ad updatedAd)
    {
        var localAds = await LoadLocalCatalogAsync();

        var existingAd = localAds.FirstOrDefault(
            x => x.Id == updatedAd.Id);

        if (existingAd == null)
        {
            return false;
        }

        // Aktualizujemy tylko pola, które użytkownik może edytować.
        existingAd.Title = updatedAd.Title;
        existingAd.Description = updatedAd.Description;
        existingAd.Price = updatedAd.Price;
        existingAd.Categories = updatedAd.Categories ?? [];

        await SaveLocalCatalogAsync(localAds);

        return true;
    }






    // --------------------------------------------------
    // ODCZYT ŹRÓDŁOWEGO katalog.json
    // --------------------------------------------------

    private async Task<List<SourceAd>> LoadSourceAdsAsync()
    {
        if (!File.Exists(SourceCatalogPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(
            SourceCatalogPath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var response =
            JsonSerializer.Deserialize<SourceCatalogResponse>(
                json,
                _jsonOptions);

        return response?.Data?.MyAds?.Ads?.Items ?? [];
    }


    // --------------------------------------------------
    // ODCZYT NASZEGO catalog.local.json
    // --------------------------------------------------

    private async Task<List<Ad>> LoadLocalCatalogAsync()
    {
        if (!File.Exists(LocalCatalogPath))
        {
            return [];
        }

        var json = await File.ReadAllTextAsync(
            LocalCatalogPath);

        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Ad>>(
            json,
            _jsonOptions) ?? [];
    }


    // --------------------------------------------------
    // ZAPIS catalog.local.json
    // --------------------------------------------------

    private async Task SaveLocalCatalogAsync(
        List<Ad> ads)
    {
        var json = JsonSerializer.Serialize(
            ads,
            _jsonOptions);

        await File.WriteAllTextAsync(
            LocalCatalogPath,
            json);
    }


    // --------------------------------------------------
    // CENA
    // --------------------------------------------------

    private static decimal ParsePrice(string? price)
    {
        if (string.IsNullOrWhiteSpace(price))
        {
            return 0;
        }

        return decimal.TryParse(
            price,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value)
            ? value
            : 0;
    }


    // --------------------------------------------------
    // MODELE ŹRÓDŁOWEGO JSON
    // --------------------------------------------------

    private class SourceCatalogResponse
    {
        public SourceData? Data { get; set; }
    }


    private class SourceData
    {
        public SourceMyAds? MyAds { get; set; }
    }


    private class SourceMyAds
    {
        public SourceAds? Ads { get; set; }
    }


    private class SourceAds
    {
        public List<SourceAd>? Items { get; set; }
    }


    private class SourceAd
    {
        public long Id { get; set; }

        public string? Title { get; set; }

        public string? Price { get; set; }

        public List<string>? Photos { get; set; }

        public List<string>? Categories { get; set; }

        public DateTime? ActivatedAt { get; set; }
    }
}

