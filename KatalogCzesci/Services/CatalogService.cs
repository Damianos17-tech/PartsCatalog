
using System.Text.Json;
using Microsoft.AspNetCore.Components.Forms;
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


    // ==================================================
    // ŚCIEŻKI
    // ==================================================

    private string SourceCatalogPath =>
        Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "katalog.json");

    private string OlxCatalogPath =>
        Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "ads-from-olx.json");

    private string LocalCatalogPath =>
        Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "ads-local.json");


    // ==================================================
    // OGŁOSZENIA OLX
    // ==================================================

    public async Task<List<Ad>> GetOlxAdsAsync()
    {
        await ImportNewOlxAdsAsync();
        await FillMissingOlxDataAsync();

        return await LoadOlxAdsAsync();
    }


    // ==================================================
    // OGŁOSZENIA LOKALNE / ADMIN
    // ==================================================

    public async Task<List<Ad>> GetLocalAdsAsync()
    {
        return await LoadLocalAdsAsync();
    }


    // ==================================================
    // IMPORT NOWYCH OGŁOSZEŃ OLX
    // ==================================================

    public async Task<int> ImportNewOlxAdsAsync()
    {
        var sourceAds =
            await LoadSourceAdsAsync();

        var olxAds =
            await LoadOlxAdsAsync();


        var existingOlxIds = olxAds
            .Where(ad => ad.OlxId.HasValue)
            .Select(ad => ad.OlxId!.Value)
            .ToHashSet();


        var addedCount = 0;


        foreach (var sourceAd in sourceAds)
        {
            // Ogłoszenie już jest w naszym
            // lokalnym katalogu OLX.
            if (existingOlxIds.Contains(sourceAd.Id))
            {
                continue;
            }


            var localAd = new Ad
            {
                Id = Guid.NewGuid(),

                OlxId = sourceAd.Id,

                Title = sourceAd.Title ?? "",

                Description = "",

                Price = ParsePrice(sourceAd.Price),

                Photos = sourceAd.Photos ?? [],

                Categories = sourceAd.Categories ?? [],

                ActivatedAt = sourceAd.ActivatedAt,

                Status = AdStatus.Active
            };


            olxAds.Add(localAd);

            existingOlxIds.Add(sourceAd.Id);

            addedCount++;
        }


        if (addedCount > 0)
        {
            await SaveOlxAdsAsync(olxAds);
        }


        return addedCount;
    }


    // ==================================================
    // UZUPEŁNIANIE BRAKUJĄCYCH DANYCH OLX
    // ==================================================

    public async Task<int> FillMissingOlxDataAsync()
    {
        var sourceAds =
            await LoadSourceAdsAsync();

        var olxAds =
            await LoadOlxAdsAsync();


        var sourceByOlxId =
            sourceAds.ToDictionary(
                ad => ad.Id);


        var updatedCount = 0;


        foreach (var localAd in olxAds)
        {
            if (!localAd.OlxId.HasValue)
            {
                continue;
            }


            if (!sourceByOlxId.TryGetValue(
                    localAd.OlxId.Value,
                    out var sourceAd))
            {
                continue;
            }


            // Na razie uzupełniamy tylko brakujące kategorie.
            if (localAd.Categories.Count == 0 &&
                sourceAd.Categories?.Count > 0)
            {
                localAd.Categories =
                    sourceAd.Categories;

                updatedCount++;
            }
        }


        if (updatedCount > 0)
        {
            await SaveOlxAdsAsync(olxAds);
        }


        return updatedCount;
    }


    // ==================================================
    // LOKALNE / ADMIN - CREATE
    // ==================================================

    public async Task<Ad> CreateLocalAdAsync(Ad ad)
    {
        var localAds =
            await LoadLocalAdsAsync();


        ad.Id = Guid.NewGuid();

        ad.OlxId = null;

        ad.Status = AdStatus.Active;

        ad.ActivatedAt ??= DateTime.Now;

        ad.Photos ??= [];

        ad.Categories ??= [];


        localAds.Add(ad);


        await SaveLocalAdsAsync(
            localAds);


        Directory.CreateDirectory(
            GetAdImageDirectory(ad));


        return ad;
    }


    // ==================================================
    // LOKALNE / ADMIN - UPDATE
    // ==================================================

    public async Task<bool> UpdateLocalAdAsync(
        Ad updatedAd)
    {
        var localAds =
            await LoadLocalAdsAsync();


        var existingAd =
            localAds.FirstOrDefault(
                x => x.Id == updatedAd.Id);


        if (existingAd == null)
        {
            return false;
        }


        existingAd.Title =
            updatedAd.Title;

        existingAd.Description =
            updatedAd.Description;

        existingAd.Price =
            updatedAd.Price;

        existingAd.Categories =
            updatedAd.Categories ?? [];

        existingAd.Photos =
            updatedAd.Photos ?? [];


        await SaveLocalAdsAsync(
            localAds);


        return true;
    }


    public async Task<bool> DeleteLocalAdAsync(Guid adId)
    {
        var localAds =
            await LoadLocalAdsAsync();

        var ad =
            localAds.FirstOrDefault(
                x => x.Id == adId);

        if (ad == null)
        {
            return false;
        }

        var imageDirectory =
            GetAdImageDirectory(ad);

        if (Directory.Exists(imageDirectory))
        {
            Directory.Delete(
                imageDirectory,
                recursive: true);
        }

        localAds.Remove(ad);

        await SaveLocalAdsAsync(
            localAds);

        return true;
    }




    public async Task<bool> UpdateOlxAdAsync(Ad updatedAd)
    {
        var olxAds =
            await LoadOlxAdsAsync();

        var existingAd =
            olxAds.FirstOrDefault(
                x => x.Id == updatedAd.Id);

        if (existingAd == null)
        {
            return false;
        }

        existingAd.Title =
            updatedAd.Title;

        existingAd.Description =
            updatedAd.Description;

        existingAd.Price =
            updatedAd.Price;

        existingAd.Categories =
            updatedAd.Categories ?? [];

        existingAd.Photos =
            updatedAd.Photos ?? [];

        await SaveOlxAdsAsync(olxAds);

        return true;
    }


    // ==================================================
    // STATUS - LOKALNE
    // ==================================================

    public async Task<bool> SetLocalAdStatusAsync(
        Guid adId,
        AdStatus status)
    {
        var localAds =
            await LoadLocalAdsAsync();


        var ad =
            localAds.FirstOrDefault(
                x => x.Id == adId);


        if (ad == null)
        {
            return false;
        }


        ad.Status = status;


        await SaveLocalAdsAsync(
            localAds);


        return true;
    }


    public async Task<bool> SetOlxAdStatusAsync(
    Guid adId,
    AdStatus status)
    {
        var olxAds =
            await LoadOlxAdsAsync();

        var ad =
            olxAds.FirstOrDefault(
                x => x.Id == adId);

        if (ad == null)
        {
            return false;
        }

        ad.Status = status;

        await SaveOlxAdsAsync(olxAds);

        return true;
    }


    // ==================================================
    // ZDJĘCIA
    // ==================================================

    public async Task<List<string>> NormalizePhotosAsync(
        Ad ad)
    {
        var directory =
            GetAdImageDirectory(ad);


        Directory.CreateDirectory(
            directory);


        var imageFiles =
            Directory
                .GetFiles(directory)
                .Where(IsSupportedImage)
                .OrderBy(GetNumericFileOrder)
                .ThenBy(Path.GetFileName)
                .ToList();


        var normalizedFiles =
            new List<string>();


        for (var i = 0;
             i < imageFiles.Count;
             i++)
        {
            var sourceFile =
                imageFiles[i];


            var targetName =
                $"{i + 1:00}.jpg";


            var targetFile =
                Path.Combine(
                    directory,
                    targetName);


            if (!string.Equals(
                    sourceFile,
                    targetFile,
                    StringComparison.OrdinalIgnoreCase))
            {
                if (File.Exists(targetFile))
                {
                    File.Delete(targetFile);
                }


                File.Move(
                    sourceFile,
                    targetFile);
            }


            normalizedFiles.Add(
                targetName);
        }


        ad.Photos =
            normalizedFiles.ToList();


        // Zapisujemy do odpowiedniego pliku.
        if (ad.OlxId.HasValue)
        {
            var olxAds =
                await LoadOlxAdsAsync();

            var storedAd =
                olxAds.FirstOrDefault(
                    x => x.Id == ad.Id);

            if (storedAd != null)
            {
                storedAd.Photos =
                    normalizedFiles.ToList();

                await SaveOlxAdsAsync(
                    olxAds);
            }
        }
        else
        {
            var localAds =
                await LoadLocalAdsAsync();

            var storedAd =
                localAds.FirstOrDefault(
                    x => x.Id == ad.Id);

            if (storedAd != null)
            {
                storedAd.Photos =
                    normalizedFiles.ToList();

                await SaveLocalAdsAsync(
                    localAds);
            }
        }


        return normalizedFiles;
    }


    // ==================================================
    // DODAJ ZDJĘCIE
    // ==================================================

    public async Task AddPhotoAsync(
        Guid adId,
        IBrowserFile file)
    {
        var olxAds =
            await LoadOlxAdsAsync();

        var localAds =
            await LoadLocalAdsAsync();


        var ad =
            olxAds.FirstOrDefault(
                x => x.Id == adId)
            ??
            localAds.FirstOrDefault(
                x => x.Id == adId);


        if (ad == null)
        {
            throw new InvalidOperationException(
                "Nie znaleziono ogłoszenia.");
        }


        var directory =
            GetAdImageDirectory(ad);


        Directory.CreateDirectory(
            directory);


        var existingFiles =
            Directory
                .GetFiles(directory)
                .Where(IsSupportedImage)
                .ToList();


        var nextNumber =
            existingFiles.Count + 1;


        var fileName =
            $"{nextNumber:00}.jpg";


        var filePath =
            Path.Combine(
                directory,
                fileName);


        await using var source =
            file.OpenReadStream(
                maxAllowedSize:
                50 * 1024 * 1024);


        await using var target =
            File.Create(
                filePath);


        await source.CopyToAsync(
            target);


        ad.Photos =
            existingFiles
                .Select(Path.GetFileName)
                .Where(x => x != null)
                .Append(fileName)
                .Cast<string>()
                .ToList();


        if (ad.OlxId.HasValue)
        {
            await SaveOlxAdsAsync(
                olxAds);
        }
        else
        {
            await SaveLocalAdsAsync(
                localAds);
        }
    }


    // ==================================================
    // USUŃ ZDJĘCIE
    // ==================================================

    public async Task<bool> DeletePhotoAsync(
        Guid adId,
        string fileName)
    {
        var olxAds =
            await LoadOlxAdsAsync();

        var localAds =
            await LoadLocalAdsAsync();


        var ad =
            olxAds.FirstOrDefault(
                x => x.Id == adId)
            ??
            localAds.FirstOrDefault(
                x => x.Id == adId);


        if (ad == null)
        {
            return false;
        }


        var directory =
            GetAdImageDirectory(ad);


        var safeFileName =
            Path.GetFileName(fileName);


        var filePath =
            Path.Combine(
                directory,
                safeFileName);


        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }


        ad.Photos.RemoveAll(
            x => string.Equals(
                x,
                safeFileName,
                StringComparison.OrdinalIgnoreCase));


        if (ad.OlxId.HasValue)
        {
            await SaveOlxAdsAsync(
                olxAds);
        }
        else
        {
            await SaveLocalAdsAsync(
                localAds);
        }


        await NormalizePhotosAsync(
            ad);


        return true;
    }


    // ==================================================
    // FOLDER ZDJĘĆ
    // ==================================================

    private string GetAdImageDirectory(
        Ad ad)
    {
        var folderId =
            ad.OlxId?.ToString()
            ??
            ad.Id.ToString();


        return Path.Combine(
            _environment.WebRootPath,
            "images",
            folderId);
    }


    // ==================================================
    // ODCZYT katalog.json
    // ==================================================

    private async Task<List<SourceAd>>
        LoadSourceAdsAsync()
    {
        if (!File.Exists(
                SourceCatalogPath))
        {
            return [];
        }


        var json =
            await File.ReadAllTextAsync(
                SourceCatalogPath);


        if (string.IsNullOrWhiteSpace(
                json))
        {
            return [];
        }


        var response =
            JsonSerializer.Deserialize<
                SourceCatalogResponse>(
                json,
                _jsonOptions);


        return response?
            .Data?
            .MyAds?
            .Ads?
            .Items
            ??
            [];
    }


    // ==================================================
    // ODCZYT ads-from-olx.json
    // ==================================================

    private async Task<List<Ad>>
        LoadOlxAdsAsync()
    {
        if (!File.Exists(
                OlxCatalogPath))
        {
            return [];
        }


        var json =
            await File.ReadAllTextAsync(
                OlxCatalogPath);


        if (string.IsNullOrWhiteSpace(
                json))
        {
            return [];
        }


        return JsonSerializer.Deserialize<
            List<Ad>>(
                json,
                _jsonOptions)
            ??
            [];
    }


    // ==================================================
    // ODCZYT ads-local.json
    // ==================================================

    private async Task<List<Ad>>
        LoadLocalAdsAsync()
    {
        if (!File.Exists(
                LocalCatalogPath))
        {
            return [];
        }


        var json =
            await File.ReadAllTextAsync(
                LocalCatalogPath);


        if (string.IsNullOrWhiteSpace(
                json))
        {
            return [];
        }


        return JsonSerializer.Deserialize<
            List<Ad>>(
                json,
                _jsonOptions)
            ??
            [];
    }


    // ==================================================
    // ZAPIS ads-from-olx.json
    // ==================================================

    private async Task SaveOlxAdsAsync(
        List<Ad> ads)
    {
        var json =
            JsonSerializer.Serialize(
                ads,
                _jsonOptions);


        await File.WriteAllTextAsync(
            OlxCatalogPath,
            json);
    }


    // ==================================================
    // ZAPIS ads-local.json
    // ==================================================

    private async Task SaveLocalAdsAsync(
        List<Ad> ads)
    {
        var json =
            JsonSerializer.Serialize(
                ads,
                _jsonOptions);


        await File.WriteAllTextAsync(
            LocalCatalogPath,
            json);
    }


    // ==================================================
    // CENA
    // ==================================================

    private static decimal ParsePrice(
        string? price)
    {
        if (string.IsNullOrWhiteSpace(
                price))
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


    // ==================================================
    // POMOCNICZE - ZDJĘCIA
    // ==================================================

    private static bool IsSupportedImage(
        string path)
    {
        var extension =
            Path.GetExtension(path)
                .ToLowerInvariant();

        return extension == ".jpg"
               ||
               extension == ".jpeg";
    }


    private static int GetNumericFileOrder(
        string path)
    {
        var name =
            Path.GetFileNameWithoutExtension(
                path);


        return int.TryParse(
            name,
            out var number)
            ? number
            : int.MaxValue;
    }


    // ==================================================
    // MODELE ŹRÓDŁOWEGO JSON
    // ==================================================

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

