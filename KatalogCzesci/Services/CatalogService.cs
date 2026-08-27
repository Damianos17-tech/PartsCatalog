using System.Globalization;
using System.Text;
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


    // ==================================================
    // CZASY BLOKAD
    // ==================================================

    private static readonly TimeSpan BackupCooldown =
        TimeSpan.FromMinutes(1);

    private static readonly TimeSpan CreateAdCooldown =
        TimeSpan.FromSeconds(5);


    // ==================================================
    // STAN BLOKAD
    // ==================================================

    private DateTime? _lastBackupTime;

    private DateTime? _lastCreateAdTime;

    private bool _cooldownStateLoaded;


    // ==================================================
    // KONSTRUKTOR
    // ==================================================

    public CatalogService(
        IWebHostEnvironment environment)
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


    private string AllegroExportPath =>
        Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "allegro-export.csv");


    private string CooldownStatePath =>
        Path.Combine(
            _environment.ContentRootPath,
            "Data",
            "backup",
            ".cooldown-state.json");


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


        var existingOlxIds =
            olxAds
                .Where(ad => ad.OlxId.HasValue)
                .Select(ad => ad.OlxId!.Value)
                .ToHashSet();


        var addedCount = 0;


        foreach (var sourceAd in sourceAds)
        {
            if (existingOlxIds.Contains(
                    sourceAd.Id))
            {
                continue;
            }


            var localAd = new Ad
            {
                Id =
                    Guid.NewGuid(),

                OlxId =
                    sourceAd.Id,

                Title =
                    sourceAd.Title ?? "",

                Description =
                    "",

                Price =
                    ParsePrice(
                        sourceAd.Price),

                // ==========================================
                // DANE ALLEGRO
                // ==========================================

                GTIN =
                    "",

                MPN =
                    "",

                ExternalId =
                    "",

                Brand =
                    "",

                Photos =
                    sourceAd.Photos ?? [],

                Categories =
                    sourceAd.Categories ?? [],

                ActivatedAt =
                    sourceAd.ActivatedAt,

                Status =
                    AdStatus.Active
            };


            olxAds.Add(localAd);

            existingOlxIds.Add(
                sourceAd.Id);

            addedCount++;
        }


        if (addedCount > 0)
        {
            await SaveOlxAdsAsync(
                olxAds);
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
            await SaveOlxAdsAsync(
                olxAds);
        }


        return updatedCount;
    }


    // ==================================================
    // LOKALNE / ADMIN - CREATE
    // ==================================================

    public async Task<Ad> CreateLocalAdAsync(
        Ad ad)
    {
        await EnsureCooldownStateLoadedAsync();


        if (GetCreateAdCooldownSeconds() > 0)
        {
            throw new InvalidOperationException(
                "Dodawanie ogłoszeń jest chwilowo zablokowane.");
        }


        var localAds =
            await LoadLocalAdsAsync();


        ad.Id =
            Guid.NewGuid();

        ad.OlxId =
            null;

        ad.Status =
            AdStatus.Active;

        ad.ActivatedAt ??=
            DateTime.Now;

        ad.GTIN ??=
            "";

        ad.MPN ??=
            "";

        ad.ExternalId ??=
            "";

        ad.Brand ??=
            "";

        ad.Photos ??=
            [];

        ad.Categories ??=
            [];


        localAds.Add(ad);


        await SaveLocalAdsAsync(
            localAds);


        Directory.CreateDirectory(
            GetAdImageDirectory(ad));


        // --------------------------------------------------
        // ZAPIS CZASU UTWORZENIA OGŁOSZENIA
        // --------------------------------------------------

        _lastCreateAdTime =
            DateTime.Now;


        await SaveCooldownStateAsync();


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


        // ==========================================
        // DANE ALLEGRO
        // ==========================================

        existingAd.GTIN =
            updatedAd.GTIN ?? "";

        existingAd.MPN =
            updatedAd.MPN ?? "";

        existingAd.ExternalId =
            updatedAd.ExternalId ?? "";

        existingAd.Brand =
            updatedAd.Brand ?? "";

        existingAd.Categories =
            updatedAd.Categories ?? [];

        existingAd.Photos =
            updatedAd.Photos ?? [];


        await SaveLocalAdsAsync(
            localAds);


        return true;
    }


    // ==================================================
    // LOKALNE / ADMIN - DELETE
    // ==================================================

    public async Task<bool> DeleteLocalAdAsync(
        Guid adId)
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


        if (Directory.Exists(
                imageDirectory))
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


    // ==================================================
    // OLX - DELETE
    // ==================================================

    public async Task<bool> DeleteOlxAdAsync(
        Guid adId)
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


        // --------------------------------------------------
        // USUNIĘCIE ZDJĘĆ
        // --------------------------------------------------

        var imageDirectory =
            GetAdImageDirectory(ad);


        if (Directory.Exists(
                imageDirectory))
        {
            Directory.Delete(
                imageDirectory,
                recursive: true);
        }


        // --------------------------------------------------
        // USUNIĘCIE OGŁOSZENIA
        // --------------------------------------------------

        olxAds.Remove(ad);


        await SaveOlxAdsAsync(
            olxAds);


        return true;
    }


    // ==================================================
    // OLX - UPDATE
    // ==================================================

    public async Task<bool> UpdateOlxAdAsync(
        Ad updatedAd)
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


        // ==========================================
        // DANE ALLEGRO
        // ==========================================

        existingAd.GTIN =
            updatedAd.GTIN ?? "";

        existingAd.MPN =
            updatedAd.MPN ?? "";

        existingAd.ExternalId =
            updatedAd.ExternalId ?? "";

        existingAd.Brand =
            updatedAd.Brand ?? "";

        existingAd.Categories =
            updatedAd.Categories ?? [];

        existingAd.Photos =
            updatedAd.Photos ?? [];


        await SaveOlxAdsAsync(
            olxAds);


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


        ad.Status =
            status;


        await SaveLocalAdsAsync(
            localAds);


        return true;
    }


    // ==================================================
    // STATUS - OLX
    // ==================================================

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


        ad.Status =
            status;


        await SaveOlxAdsAsync(
            olxAds);


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
                if (File.Exists(
                        targetFile))
                {
                    File.Delete(
                        targetFile);
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


        if (File.Exists(
                filePath))
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
    // EKSPORT ALLEGRO CSV
    // ==================================================

    public async Task<string> ExportAllegroCsvAsync()
    {
        var ads =
            await LoadLocalAdsAsync();


        var csv =
            new StringBuilder();


        // --------------------------------------------------
        // NAGŁÓWEK ALLEGRO
        // --------------------------------------------------

        csv.AppendLine(
            "GTIN,EXTERNAL_ID,NAME,STOCK,PRICE,MPN,DESCRIPTION," +
            "IMAGE1,IMAGE2,IMAGE3,IMAGE4,IMAGE5,IMAGE6,IMAGE7,IMAGE8," +
            "IMAGE9,IMAGE10,IMAGE11,IMAGE12,IMAGE13,IMAGE14,IMAGE15,IMAGE16," +
            "AI_COCREATED,CATEGORY,BRAND,COLOR,SIZE,MATERIAL");


        // --------------------------------------------------
        // OGŁOSZENIA
        // --------------------------------------------------

        foreach (var ad in ads)
        {
            var values =
                new List<string>
                {
                    // GTIN
                    CsvEscape(ad.GTIN),

                    // EXTERNAL_ID
                    CsvEscape(ad.ExternalId),

                    // NAME
                    CsvEscape(ad.Title),

                    // STOCK
                    "1",

                    // PRICE
                    CsvEscape(
                        ad.Price.ToString(
                            "0.##",
                            CultureInfo.InvariantCulture)),

                    // MPN
                    CsvEscape(ad.MPN),

                    // DESCRIPTION
                    CsvEscape(ad.Description)
                };


            // --------------------------------------------------
            // IMAGE1 - IMAGE16
            // --------------------------------------------------

            for (var i = 1; i <= 16; i++)
            {
                var imageUrl = "";


                if (ad.Photos != null &&
                    ad.Photos.Count >= i)
                {
                    imageUrl =
                        $"https://parts.dcplatforms.pl/" +
                        $"ogloszenie/{ad.Id}/{i:00}.jpg";
                }


                values.Add(
                    CsvEscape(imageUrl));
            }


            // --------------------------------------------------
            // AI_COCREATED
            // --------------------------------------------------

            values.Add("");


            // --------------------------------------------------
            // CATEGORY
            // --------------------------------------------------

            var category =
                ad.Categories != null
                    ? string.Join(
                        " / ",
                        ad.Categories)
                    : "";


            values.Add(
                CsvEscape(category));


            // --------------------------------------------------
            // BRAND
            // --------------------------------------------------

            values.Add(
                CsvEscape(ad.Brand));


            // --------------------------------------------------
            // COLOR
            // --------------------------------------------------

            values.Add("");


            // --------------------------------------------------
            // SIZE
            // --------------------------------------------------

            values.Add("");


            // --------------------------------------------------
            // MATERIAL
            // --------------------------------------------------

            values.Add("");


            csv.AppendLine(
                string.Join(
                    ",",
                    values));
        }


        // --------------------------------------------------
        // ZAPIS PLIKU
        // --------------------------------------------------

        var directory =
            Path.GetDirectoryName(
                AllegroExportPath);


        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }


        await File.WriteAllTextAsync(
            AllegroExportPath,
            csv.ToString(),
            new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: true));


        return AllegroExportPath;
    }


    // ==================================================
    // CSV - ESCAPOWANIE
    // ==================================================

    private static string CsvEscape(
        string? value)
    {
        if (string.IsNullOrEmpty(
                value))
        {
            return "";
        }


        if (value.Contains('"') ||
            value.Contains(',') ||
            value.Contains('\r') ||
            value.Contains('\n'))
        {
            return
                "\"" +
                value.Replace(
                    "\"",
                    "\"\"") +
                "\"";
        }


        return value;
    }


    // ==================================================
    // FOLDER ZDJĘĆ
    // ==================================================

    private string GetAdImageDirectory(
        Ad ad)
    {
        return Path.Combine(
            _environment.WebRootPath,
            "images",
            ad.Id.ToString());
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


        var ads =
            JsonSerializer.Deserialize<
                List<Ad>>(
                    json,
                    _jsonOptions)
            ??
            [];


        // Lokalne ogłoszenia są całkowicie
        // niezależne od OLX.

        foreach (var ad in ads)
        {
            ad.OlxId = null;
        }


        return ads;
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
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
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


    // ==================================================
    // BACKUP - SPRAWDZENIE CZASU
    // ==================================================

    public int GetBackupCooldownSeconds()
    {
        EnsureCooldownStateLoaded();


        if (!_lastBackupTime.HasValue)
        {
            return 0;
        }


        var elapsed =
            DateTime.Now -
            _lastBackupTime.Value;


        var remaining =
            BackupCooldown -
            elapsed;


        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }


        return (int)Math.Ceiling(
            remaining.TotalSeconds);
    }


    // ==================================================
    // DODAWANIE - SPRAWDZENIE CZASU
    // ==================================================

    public int GetCreateAdCooldownSeconds()
    {
        EnsureCooldownStateLoaded();


        if (!_lastCreateAdTime.HasValue)
        {
            return 0;
        }


        var elapsed =
            DateTime.Now -
            _lastCreateAdTime.Value;


        var remaining =
            CreateAdCooldown -
            elapsed;


        if (remaining <= TimeSpan.Zero)
        {
            return 0;
        }


        return (int)Math.Ceiling(
            remaining.TotalSeconds);
    }


    // ==================================================
    // BACKUP BAZY
    // ==================================================

    public async Task<bool> BackupDatabaseAsync()
    {
        await EnsureCooldownStateLoadedAsync();


        // --------------------------------------------------
        // SPRAWDZENIE BLOKADY
        // --------------------------------------------------

        if (GetBackupCooldownSeconds() > 0)
        {
            return false;
        }


        // --------------------------------------------------
        // FOLDER BACKUP
        // --------------------------------------------------

        var backupDirectory =
            Path.Combine(
                _environment.ContentRootPath,
                "Data",
                "backup");


        Directory.CreateDirectory(
            backupDirectory);


        // --------------------------------------------------
        // TIMESTAMP
        // --------------------------------------------------

        var timestamp =
            DateTime.Now.ToString(
                "yyyyMMddHHmmss");


        // --------------------------------------------------
        // BACKUP OLX
        // --------------------------------------------------

        await BackupFileAsync(
            OlxCatalogPath,
            backupDirectory,
            timestamp);


        // --------------------------------------------------
        // BACKUP LOCAL
        // --------------------------------------------------

        await BackupFileAsync(
            LocalCatalogPath,
            backupDirectory,
            timestamp);


        // --------------------------------------------------
        // ZAPIS CZASU BACKUPU
        // --------------------------------------------------

        _lastBackupTime =
            DateTime.Now;


        await SaveCooldownStateAsync();


        return true;
    }


    // ==================================================
    // BACKUP POJEDYNCZEGO PLIKU
    // ==================================================

    private static async Task BackupFileAsync(
        string sourcePath,
        string backupDirectory,
        string timestamp)
    {
        if (!File.Exists(
                sourcePath))
        {
            return;
        }


        var fileName =
            Path.GetFileName(
                sourcePath);


        var backupFileName =
            $"{fileName}_backup{timestamp}";


        var backupPath =
            Path.Combine(
                backupDirectory,
                backupFileName);


        await using var source =
            File.OpenRead(
                sourcePath);


        await using var destination =
            File.Create(
                backupPath);


        await source.CopyToAsync(
            destination);
    }


    // ==================================================
    // WCZYTANIE STANU BLOKAD
    // ==================================================

    private void EnsureCooldownStateLoaded()
    {
        if (_cooldownStateLoaded)
        {
            return;
        }


        _cooldownStateLoaded = true;


        if (!File.Exists(
                CooldownStatePath))
        {
            return;
        }


        try
        {
            var json =
                File.ReadAllText(
                    CooldownStatePath);


            if (string.IsNullOrWhiteSpace(
                    json))
            {
                return;
            }


            var state =
                JsonSerializer.Deserialize<
                    CooldownState>(
                    json,
                    _jsonOptions);


            _lastBackupTime =
                state?.LastBackupTime;

            _lastCreateAdTime =
                state?.LastCreateAdTime;
        }
        catch
        {
            // Jeżeli plik stanu jest uszkodzony,
            // aplikacja może działać dalej.
        }
    }


    private async Task EnsureCooldownStateLoadedAsync()
    {
        if (_cooldownStateLoaded)
        {
            return;
        }


        _cooldownStateLoaded = true;


        if (!File.Exists(
                CooldownStatePath))
        {
            return;
        }


        try
        {
            var json =
                await File.ReadAllTextAsync(
                    CooldownStatePath);


            if (string.IsNullOrWhiteSpace(
                    json))
            {
                return;
            }


            var state =
                JsonSerializer.Deserialize<
                    CooldownState>(
                json,
                _jsonOptions);


            _lastBackupTime =
                state?.LastBackupTime;

            _lastCreateAdTime =
                state?.LastCreateAdTime;
        }
        catch
        {
            // Ignorujemy uszkodzony plik stanu.
        }
    }


    // ==================================================
    // ZAPIS STANU BLOKAD
    // ==================================================

    private async Task SaveCooldownStateAsync()
    {
        var directory =
            Path.GetDirectoryName(
                CooldownStatePath);


        if (!string.IsNullOrWhiteSpace(
                directory))
        {
            Directory.CreateDirectory(
                directory);
        }


        var state =
            new CooldownState
            {
                LastBackupTime =
                    _lastBackupTime,

                LastCreateAdTime =
                    _lastCreateAdTime
            };


        var json =
            JsonSerializer.Serialize(
                state,
                _jsonOptions);


        await File.WriteAllTextAsync(
            CooldownStatePath,
            json);
    }


    // ==================================================
    // MODEL STANU BLOKAD
    // ==================================================

    private class CooldownState
    {
        public DateTime? LastBackupTime { get; set; }

        public DateTime? LastCreateAdTime { get; set; }
    }













    // ==================================================
    // JEDNORAZOWA MIGRACJA OLX -> LOCAL
    // ==================================================

    public async Task MigrateOlxAdsToLocalAsync()
    {
        var olxAds =
            await LoadOlxAdsAsync();

        var localAds =
            new List<Ad>();


        foreach (var olxAd in olxAds)
        {
            // ------------------------------------------
            // STARE ID
            // ------------------------------------------

            var oldId =
                olxAd.Id;


            // ------------------------------------------
            // NOWE ID LOCAL
            // ------------------------------------------

            var newId =
                Guid.NewGuid();


            // ------------------------------------------
            // KOPIA OGŁOSZENIA
            // ------------------------------------------

            var localAd =
                new Ad
                {
                    Id =
                        newId,

                    OlxId =
                        null,

                    Title =
                        olxAd.Title,

                    Description =
                        olxAd.Description,

                    Price =
                        olxAd.Price,

                    GTIN =
                        olxAd.GTIN ?? "",

                    MPN =
                        olxAd.MPN ?? "",

                    ExternalId =
                        olxAd.ExternalId ?? "",

                    Brand =
                        olxAd.Brand ?? "",

                    Photos =
                        olxAd.Photos?.ToList() ?? [],

                    Categories =
                        olxAd.Categories?.ToList() ?? [],

                    ActivatedAt =
                        olxAd.ActivatedAt,

                    Status =
                        olxAd.Status
                };


            localAds.Add(localAd);


            // ------------------------------------------
            // STARY FOLDER ZDJĘĆ
            // ------------------------------------------

            var oldDirectory =
                Path.Combine(
                    _environment.WebRootPath,
                    "images",
                    oldId.ToString());


            // ------------------------------------------
            // NOWY FOLDER ZDJĘĆ
            // ------------------------------------------

            var newDirectory =
                Path.Combine(
                    _environment.WebRootPath,
                    "images",
                    newId.ToString());


            // ------------------------------------------
            // KOPIUJEMY ZDJĘCIA
            // ------------------------------------------

            if (Directory.Exists(oldDirectory))
            {
                Directory.CreateDirectory(
                    newDirectory);


                foreach (var file in Directory.GetFiles(
                             oldDirectory))
                {
                    var fileName =
                        Path.GetFileName(file);

                    var destination =
                        Path.Combine(
                            newDirectory,
                            fileName);

                    File.Copy(
                        file,
                        destination);
                }
            }
        }


        // ------------------------------------------
        // ZAPIS ads-local.json
        // ------------------------------------------

        await SaveLocalAdsAsync(
            localAds);
    }


}