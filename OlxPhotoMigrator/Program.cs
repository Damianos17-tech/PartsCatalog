using System.Text.Json;

Console.WriteLine("==============================================");
Console.WriteLine("       OLX PHOTO MIGRATOR");
Console.WriteLine("==============================================");
Console.WriteLine();


// ==================================================
// USTALENIE KATALOGU GŁÓWNEGO PROJEKTU
// ==================================================

var currentDirectory =
    AppContext.BaseDirectory;


// Szukamy katalogu KatalogCzesci,
// cofając się po drzewie katalogów.

string projectDirectory = Path.Combine(
    Directory.GetParent(AppContext.BaseDirectory)!.Parent!.Parent!.Parent!.Parent!.FullName,
    "KatalogCzesci");


if (projectDirectory == null)
{
    Console.WriteLine(
        "Nie znaleziono katalogu projektu KatalogCzesci.");

    Console.WriteLine();
    Console.WriteLine(
        "Uruchom skrypt z katalogu projektu.");

    return;
}


var jsonPath =
    Path.Combine(
        projectDirectory,
        "Data",
        "ads-from-olx.json");


var imagesDirectory =
    Path.Combine(
        projectDirectory,
        "wwwroot",
        "images");


Console.WriteLine(
    $"Projekt: {projectDirectory}");

Console.WriteLine(
    $"JSON:    {jsonPath}");

Console.WriteLine(
    $"Zdjęcia: {imagesDirectory}");

Console.WriteLine();


if (!File.Exists(jsonPath))
{
    Console.WriteLine(
        "!!! NIE ZNALEZIONO ads-from-olx.json !!!");

    return;
}


// ==================================================
// WCZYTANIE JSON
// ==================================================

Console.WriteLine(
    "Wczytywanie ads-from-olx.json...");

var json =
    await File.ReadAllTextAsync(jsonPath);


var options =
    new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };


var ads =
    JsonSerializer.Deserialize<List<Ad>>(
        json,
        options)
    ?? [];


Console.WriteLine(
    $"Liczba ogłoszeń: {ads.Count}");

Console.WriteLine();


// ==================================================
// ANALIZA
// ==================================================

var adsWithOlxPhotos = 0;
var urlsCount = 0;
var localPhotosCount = 0;


foreach (var ad in ads)
{
    if (ad.Photos == null ||
        ad.Photos.Count == 0)
    {
        continue;
    }


    var urlPhotos =
        ad.Photos
            .Count(IsUrl);


    var localPhotos =
        ad.Photos.Count(
            x => !IsUrl(x));


    if (urlPhotos > 0)
    {
        adsWithOlxPhotos++;
    }


    urlsCount +=
        urlPhotos;

    localPhotosCount +=
        localPhotos;
}


Console.WriteLine("==============================================");
Console.WriteLine("ANALIZA");
Console.WriteLine("==============================================");

Console.WriteLine(
    $"Ogłoszenia z URL zdjęć: {adsWithOlxPhotos}");

Console.WriteLine(
    $"Zdjęcia do pobrania:    {urlsCount}");

Console.WriteLine(
    $"Już lokalne zdjęcia:    {localPhotosCount}");

Console.WriteLine();


// ==================================================
// POTWIERDZENIE
// ==================================================

Console.WriteLine(
    "Migrator zmodyfikuje WYŁĄCZNIE:");

Console.WriteLine(
    "  Data/ads-from-olx.json");

Console.WriteLine(
    "oraz utworzy pliki w:");

Console.WriteLine(
    "  wwwroot/images/{Id}/");

Console.WriteLine();

Console.WriteLine(
    "ads-local.json NIE zostanie zmieniony.");

Console.WriteLine();

Console.Write(
    "Rozpocząć migrację? [T/N]: ");

var answer =
    Console.ReadLine();


if (!string.Equals(
        answer,
        "T",
        StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine();
    Console.WriteLine(
        "Anulowano.");

    return;
}


Console.WriteLine();


// ==================================================
// BACKUP
// ==================================================

var timestamp =
    DateTime.Now.ToString(
        "yyyyMMdd_HHmmss");


var backupPath =
    Path.Combine(
        Path.GetDirectoryName(jsonPath)!,
        $"ads-from-olx_backup_{timestamp}.json");


File.Copy(
    jsonPath,
    backupPath);


Console.WriteLine(
    $"Backup utworzony:");

Console.WriteLine(
    backupPath);

Console.WriteLine();


// ==================================================
// HTTP CLIENT
// ==================================================

using var httpClient =
    new HttpClient();

httpClient.Timeout =
    TimeSpan.FromSeconds(60);

httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
    "Mozilla/5.0");


// ==================================================
// MIGRACJA
// ==================================================

var downloadedCount = 0;
var skippedCount = 0;
var failedCount = 0;
var processedAds = 0;


foreach (var ad in ads)
{
    processedAds++;


    if (ad.Photos == null ||
        ad.Photos.Count == 0)
    {
        continue;
    }


    Console.WriteLine(
        $"[{processedAds}/{ads.Count}] {ad.Title}");

    Console.WriteLine(
        $"ID: {ad.Id}");


    var adDirectory =
        Path.Combine(
            imagesDirectory,
            ad.Id.ToString());


    Directory.CreateDirectory(
        adDirectory);


    var newPhotos =
        new List<string>();


    for (var i = 0;
         i < ad.Photos.Count;
         i++)
    {
        var photo =
            ad.Photos[i];


        // ==========================================
        // ZDJĘCIE JUŻ LOKALNE
        // ==========================================

        if (!IsUrl(photo))
        {
            var localFileName =
                Path.GetFileName(photo);


            if (!string.IsNullOrWhiteSpace(
                    localFileName))
            {
                newPhotos.Add(
                    localFileName);

                Console.WriteLine(
                    $"  [LOCAL] {localFileName}");
            }


            continue;
        }


        // ==========================================
        // NAZWA PLIKU
        // ==========================================

        var fileName =
            $"{newPhotos.Count + 1:00}.jpg";


        var filePath =
            Path.Combine(
                adDirectory,
                fileName);


        // ==========================================
        // JEŻELI PLIK JUŻ ISTNIEJE
        // ==========================================

        if (File.Exists(filePath))
        {
            Console.WriteLine(
                $"  [ISTNIEJE] {fileName}");

            newPhotos.Add(
                fileName);

            skippedCount++;

            continue;
        }


        // ==========================================
        // POBIERANIE
        // ==========================================

        try
        {
            Console.WriteLine(
                $"  [POBIERAM] {fileName}");

            var bytes =
                await httpClient.GetByteArrayAsync(
                    photo);


            await File.WriteAllBytesAsync(
                filePath,
                bytes);


            Console.WriteLine(
                $"  [OK] {fileName} ({bytes.Length:N0} B)");

            newPhotos.Add(
                fileName);

            downloadedCount++;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"  [BŁĄD] {ex.Message}");

            failedCount++;

            // Nie dodajemy nieudanego zdjęcia
            // do Photos.
        }
    }


    // ==========================================
    // AKTUALIZACJA PHOTOS
    // ==========================================

    ad.Photos =
        newPhotos;

    Console.WriteLine();
}


// ==================================================
// ZAPIS JSON
// ==================================================

Console.WriteLine(
    "Zapisywanie ads-from-olx.json...");

var outputJson =
    JsonSerializer.Serialize(
        ads,
        options);


await File.WriteAllTextAsync(
    jsonPath,
    outputJson);


// ==================================================
// PODSUMOWANIE
// ==================================================

Console.WriteLine();

Console.WriteLine("==============================================");
Console.WriteLine("              MIGRACJA GOTOWA");
Console.WriteLine("==============================================");

Console.WriteLine(
    $"Pobrano zdjęć:       {downloadedCount}");

Console.WriteLine(
    $"Pominięto istniejących: {skippedCount}");

Console.WriteLine(
    $"Błędów pobierania:   {failedCount}");

Console.WriteLine();

Console.WriteLine(
    $"Backup:");

Console.WriteLine(
    backupPath);

Console.WriteLine();

Console.WriteLine(
    "ads-from-olx.json został zaktualizowany.");

Console.WriteLine();


// ==================================================
// FUNKCJE
// ==================================================

static bool IsUrl(
    string? value)
{
    return
        !string.IsNullOrWhiteSpace(value) &&
        (
            value.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase)
            ||
            value.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase)
        );
}


static string? FindProjectDirectory(
    string startDirectory)
{
    var directory =
        new DirectoryInfo(
            startDirectory);


    while (directory != null)
    {
        var dataDirectory =
            Path.Combine(
                directory.FullName,
                "Data");


        var wwwrootDirectory =
            Path.Combine(
                directory.FullName,
                "wwwroot");


        var projectFile =
            Path.Combine(
                directory.FullName,
                "KatalogCzesci.csproj");


        if (Directory.Exists(dataDirectory) &&
            Directory.Exists(wwwrootDirectory) &&
            File.Exists(projectFile))
        {
            return directory.FullName;
        }


        directory =
            directory.Parent;
    }


    return null;
}


// ==================================================
// MODEL
// ==================================================

public class Ad
{
    public Guid Id { get; set; }

    public long? OlxId { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public decimal Price { get; set; }

    public string? GTIN { get; set; }

    public string? ExternalId { get; set; }

    public string? MPN { get; set; }

    public string? Brand { get; set; }

    public List<string> Photos { get; set; } = [];

    public List<string> Categories { get; set; } = [];

    public DateTime? ActivatedAt { get; set; }

    public int Status { get; set; }
}