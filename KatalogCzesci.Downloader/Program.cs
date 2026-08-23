using System.Text.Json;
using KatalogCzesci.Models;

Console.WriteLine("================================");
Console.WriteLine("      KATALOG CZĘŚCI");
Console.WriteLine("      IMAGE DOWNLOADER");
Console.WriteLine("================================");
Console.WriteLine();

string projectRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "KatalogCzesci")
);

string jsonPath = Path.Combine(
    projectRoot,
    "Data",
    "katalog.json"
);

string imagesPath = Path.Combine(
    projectRoot,
    "Images"
);

Console.WriteLine($"Katalog projektu:");
Console.WriteLine(projectRoot);
Console.WriteLine();

if (!File.Exists(jsonPath))
{
    Console.WriteLine("Nie znaleziono pliku:");
    Console.WriteLine(jsonPath);
    Console.WriteLine();
    Console.WriteLine("Naciśnij Enter, aby zakończyć...");
    Console.ReadLine();
    return;
}

Console.WriteLine($"Znaleziono katalog:");
Console.WriteLine(jsonPath);
Console.WriteLine();

string json = await File.ReadAllTextAsync(jsonPath);

var options = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

var response = JsonSerializer.Deserialize<CatalogResponse>(
    json,
    options
);

var ads = response?.Data?.MyAds?.Ads?.Items ?? [];

Console.WriteLine($"Znaleziono ogłoszeń: {ads.Count}");
Console.WriteLine();

Directory.CreateDirectory(imagesPath);

using HttpClient httpClient = new();

int totalPhotos = 0;
int downloadedPhotos = 0;
int skippedPhotos = 0;
int failedPhotos = 0;

foreach (var ad in ads)
{
    Console.WriteLine("--------------------------------");
    Console.WriteLine($"ID: {ad.Id}");
    Console.WriteLine($"Tytuł: {ad.Title}");
    Console.WriteLine($"Zdjęć: {ad.Photos?.Count ?? 0}");
    Console.WriteLine();

    if (ad.Photos == null || ad.Photos.Count == 0)
    {
        continue;
    }

    string adDirectory = Path.Combine(
        imagesPath,
        ad.Id.ToString()
    );

    Directory.CreateDirectory(adDirectory);

    for (int i = 0; i < ad.Photos.Count; i++)
    {
        string photoUrl = ad.Photos[i];

        totalPhotos++;

        string fileName = $"{i + 1:00}.jpg";

        string filePath = Path.Combine(
            adDirectory,
            fileName
        );

        Console.Write($"  [{i + 1}/{ad.Photos.Count}] ");

        if (File.Exists(filePath))
        {
            Console.WriteLine($"POMINIĘTO — {fileName}");
            skippedPhotos++;
            continue;
        }

        try
        {
            byte[] imageBytes = await httpClient.GetByteArrayAsync(photoUrl);

            await File.WriteAllBytesAsync(
                filePath,
                imageBytes
            );

            Console.WriteLine($"POBRANO — {fileName}");

            downloadedPhotos++;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"BŁĄD — {ex.Message}");

            failedPhotos++;
        }
    }
}

Console.WriteLine();
Console.WriteLine("================================");
Console.WriteLine("          ZAKOŃCZONO");
Console.WriteLine("================================");
Console.WriteLine();

Console.WriteLine($"Ogłoszenia:          {ads.Count}");
Console.WriteLine($"Zdjęcia znalezione:  {totalPhotos}");
Console.WriteLine($"Pobrane:             {downloadedPhotos}");
Console.WriteLine($"Pominięte:            {skippedPhotos}");
Console.WriteLine($"Błędy:               {failedPhotos}");

Console.WriteLine();
Console.WriteLine("Naciśnij Enter, aby zakończyć...");
Console.ReadLine();