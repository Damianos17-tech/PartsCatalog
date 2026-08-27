using System.Text.Json;
using KatalogCzesci.Models;

Console.WriteLine("================================");
Console.WriteLine("      KATALOG CZĘŚCI");
Console.WriteLine("      IMAGE DOWNLOADER");
Console.WriteLine("================================");
Console.WriteLine();


// ==================================================
// KATALOG GŁÓWNY PROJEKTU
// ==================================================

string projectRoot = Path.GetFullPath(
    Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        "..",
        "KatalogCzesci"
    )
);


Console.WriteLine("Katalog projektu:");
Console.WriteLine(projectRoot);
Console.WriteLine();


// ==================================================
// ŹRÓDŁO - ads-from-olx.json
// ==================================================

string jsonPath = Path.Combine(
    projectRoot,
    "Data",
    "ads-from-olx.json"
);


// ==================================================
// STARY FOLDER ZDJĘĆ
//
// wwwroot/images/{OLX_ID}/01.jpg
// ==================================================

string oldImagesPath = Path.Combine(
    projectRoot,
    "wwwroot",
    "images"
);


// ==================================================
// NOWY FOLDER ZDJĘĆ
//
// wwwroot/images2/{GUID}/01.jpg
// ==================================================

string newImagesPath = Path.Combine(
    projectRoot,
    "wwwroot",
    "images2"
);


Console.WriteLine("Źródło JSON:");
Console.WriteLine(jsonPath);
Console.WriteLine();

Console.WriteLine("Stary folder zdjęć:");
Console.WriteLine(oldImagesPath);
Console.WriteLine();

Console.WriteLine("Nowy folder zdjęć:");
Console.WriteLine(newImagesPath);
Console.WriteLine();


// ==================================================
// SPRAWDZENIE PLIKU JSON
// ==================================================

if (!File.Exists(jsonPath))
{
    Console.WriteLine("❌ Nie znaleziono pliku:");
    Console.WriteLine(jsonPath);

    Console.WriteLine();
    Console.WriteLine("Naciśnij Enter, aby zakończyć...");

    Console.ReadLine();

    return;
}


Console.WriteLine("✅ Znaleziono ads-from-olx.json");
Console.WriteLine();


// ==================================================
// ODCZYT JSON
// ==================================================

string json =
    await File.ReadAllTextAsync(
        jsonPath
    );


var options =
    new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    };


var ads =
    JsonSerializer.Deserialize<List<Ad>>(
        json,
        options
    )
    ?? [];


Console.WriteLine(
    $"Znaleziono ogłoszeń: {ads.Count}"
);

Console.WriteLine();


// ==================================================
// TWORZENIE FOLDERU images2
// ==================================================

Directory.CreateDirectory(
    newImagesPath
);


// ==================================================
// HTTP CLIENT
// ==================================================

using HttpClient httpClient =
    new();


// ==================================================
// STATYSTYKI
// ==================================================

int totalPhotos = 0;

int downloadedPhotos = 0;

int copiedPhotos = 0;

int skippedPhotos = 0;

int failedPhotos = 0;


// ==================================================
// PRZETWARZANIE OGŁOSZEŃ
// ==================================================

foreach (var ad in ads)
{
    Console.WriteLine("--------------------------------");

    Console.WriteLine(
        $"GUID: {ad.Id}"
    );

    Console.WriteLine(
        $"OLX ID: {ad.OlxId}"
    );

    Console.WriteLine(
        $"Tytuł: {ad.Title}"
    );

    Console.WriteLine(
        $"Zdjęć: {ad.Photos?.Count ?? 0}"
    );

    Console.WriteLine();


    // ==================================================
    // BRAK ZDJĘĆ
    // ==================================================

    if (ad.Photos == null ||
        ad.Photos.Count == 0)
    {
        Console.WriteLine(
            "  Brak zdjęć."
        );

        continue;
    }


    // ==================================================
    // NOWY FOLDER DLA OGŁOSZENIA
    //
    // images2/{GUID}/
    // ==================================================

    string newAdDirectory =
        Path.Combine(
            newImagesPath,
            ad.Id.ToString()
        );


    Directory.CreateDirectory(
        newAdDirectory
    );


    // ==================================================
    // STARY FOLDER OGŁOSZENIA
    //
    // images/{OLX_ID}/
    // ==================================================

    string? oldAdDirectory =
        ad.OlxId.HasValue
            ? Path.Combine(
                oldImagesPath,
                ad.OlxId.Value.ToString()
            )
            : null;


    // ==================================================
    // PRZETWARZANIE ZDJĘĆ
    // ==================================================

    for (int i = 0;
         i < ad.Photos.Count;
         i++)
    {
        string photoSource =
            ad.Photos[i];


        totalPhotos++;


        // ==============================================
        // NOWA NAZWA
        //
        // 01.jpg
        // 02.jpg
        // itd.
        // ==============================================

        string fileName =
            $"{i + 1:00}.jpg";


        string newFilePath =
            Path.Combine(
                newAdDirectory,
                fileName
            );


        Console.Write(
            $"  [{i + 1}/{ad.Photos.Count}] "
        );


        // ==============================================
        // JEŚLI JUŻ ISTNIEJE
        // ==============================================

        if (File.Exists(newFilePath))
        {
            Console.WriteLine(
                $"POMINIĘTO — {fileName}"
            );

            skippedPhotos++;

            continue;
        }


        try
        {
            // ==========================================
            // PRZYPADEK 1
            //
            // Pełny URL:
            //
            // https://...
            // ==========================================

            if (Uri.TryCreate(
                    photoSource,
                    UriKind.Absolute,
                    out var photoUri)
                &&
                (photoUri.Scheme == Uri.UriSchemeHttp ||
                 photoUri.Scheme == Uri.UriSchemeHttps))
            {
                byte[] imageBytes =
                    await httpClient.GetByteArrayAsync(
                        photoUri
                    );


                await File.WriteAllBytesAsync(
                    newFilePath,
                    imageBytes
                );


                Console.WriteLine(
                    $"POBRANO — {fileName}"
                );

                downloadedPhotos++;

                continue;
            }


            // ==========================================
            // PRZYPADEK 2
            //
            // W JSON jest tylko np.:
            //
            // 01.jpg
            // 02.jpg
            //
            // Wtedy kopiujemy ze starego folderu:
            //
            // images/{OLX_ID}/01.jpg
            // ==========================================

            if (!string.IsNullOrWhiteSpace(
                    oldAdDirectory))
            {
                string oldFileName =
                    Path.GetFileName(
                        photoSource
                    );


                string oldFilePath =
                    Path.Combine(
                        oldAdDirectory,
                        oldFileName
                    );


                if (File.Exists(oldFilePath))
                {
                    File.Copy(
                        oldFilePath,
                        newFilePath,
                        overwrite: false
                    );


                    Console.WriteLine(
                        $"SKOPIOWANO — {fileName}"
                    );

                    copiedPhotos++;

                    continue;
                }
            }


            // ==========================================
            // NIE ZNALEZIONO
            // ==========================================

            Console.WriteLine(
                $"❌ NIE ZNALEZIONO ŹRÓDŁA — {photoSource}"
            );

            failedPhotos++;
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"BŁĄD — {ex.Message}"
            );

            failedPhotos++;
        }
    }
}


// ==================================================
// PODSUMOWANIE
// ==================================================

Console.WriteLine();

Console.WriteLine("================================");
Console.WriteLine("          ZAKOŃCZONO");
Console.WriteLine("================================");
Console.WriteLine();

Console.WriteLine(
    $"Ogłoszenia:          {ads.Count}"
);

Console.WriteLine(
    $"Zdjęcia znalezione:  {totalPhotos}"
);

Console.WriteLine(
    $"Pobrane z internetu: {downloadedPhotos}"
);

Console.WriteLine(
    $"Skopiowane:          {copiedPhotos}"
);

Console.WriteLine(
    $"Pominięte:           {skippedPhotos}"
);

Console.WriteLine(
    $"Błędy:               {failedPhotos}"
);

Console.WriteLine();

Console.WriteLine(
    "Nowe zdjęcia zapisano w:"
);

Console.WriteLine(
    newImagesPath
);

Console.WriteLine();

Console.WriteLine(
    "Naciśnij Enter, aby zakończyć..."
);

Console.ReadLine();