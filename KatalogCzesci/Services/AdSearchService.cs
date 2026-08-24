
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using KatalogCzesci.Models;

namespace KatalogCzesci.Services;

public class AdSearchService
{
    public List<Ad> Search(
        IEnumerable<Ad> ads,
        string? query)
    {
        var source =
            ads?.ToList()
            ?? [];

        if (string.IsNullOrWhiteSpace(query))
        {
            return source;
        }


        var normalizedQuery =
            Normalize(query);

        var queryWords =
            normalizedQuery
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries);


        if (queryWords.Length == 0)
        {
            return source;
        }


        var results =
            new List<SearchResult>();


        foreach (var ad in source)
        {
            var score =
                CalculateScore(
                    ad,
                    normalizedQuery,
                    queryWords);


            if (score > 0)
            {
                results.Add(
                    new SearchResult(
                        ad,
                        score));
            }
        }


        return results
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Ad.Title)
            .Select(x => x.Ad)
            .ToList();
    }


    // =========================================================
    // RANKING
    // =========================================================

    private static int CalculateScore(
        Ad ad,
        string normalizedQuery,
        string[] queryWords)
    {
        var title =
            Normalize(ad.Title);

        var description =
            Normalize(ad.Description);

        var categories =
            Normalize(
                string.Join(
                    " ",
                    ad.Categories ?? []));


        var score = 0;


        // -----------------------------------------------------
        // DOKŁADNA FRAZA
        // -----------------------------------------------------

        if (title.Contains(normalizedQuery))
        {
            score += 100;
        }

        if (categories.Contains(normalizedQuery))
        {
            score += 60;
        }

        if (description.Contains(normalizedQuery))
        {
            score += 30;
        }


        // -----------------------------------------------------
        // POSZCZEGÓLNE SŁOWA
        // -----------------------------------------------------

        foreach (var word in queryWords)
        {
            if (string.IsNullOrWhiteSpace(word))
            {
                continue;
            }


            // Tytuł — największa waga.
            if (title.Contains(word))
            {
                score += 25;

                if (title.StartsWith(word))
                {
                    score += 10;
                }
            }


            // Kategorie.
            if (categories.Contains(word))
            {
                score += 15;
            }


            // Opis.
            if (description.Contains(word))
            {
                score += 5;
            }


            // -------------------------------------------------
            // FUZZY MATCHING
            // -------------------------------------------------

            var titleWords =
                GetWords(title);

            var categoryWords =
                GetWords(categories);

            var descriptionWords =
                GetWords(description);


            if (HasFuzzyMatch(word, titleWords))
            {
                score += 15;
            }
            else if (HasFuzzyMatch(word, categoryWords))
            {
                score += 10;
            }
            else if (HasFuzzyMatch(word, descriptionWords))
            {
                score += 3;
            }
        }


        return score;
    }


    // =========================================================
    // FUZZY MATCH
    // =========================================================

    private static bool HasFuzzyMatch(
        string queryWord,
        IEnumerable<string> candidateWords)
    {
        foreach (var candidate in candidateWords)
        {
            if (candidate == queryWord)
            {
                return true;
            }


            // Krótkie słowa nie powinny być zbyt agresywnie
            // dopasowywane.
            if (queryWord.Length < 4)
            {
                continue;
            }


            if (candidate.Length < 4)
            {
                continue;
            }


            var maxDistance =
                queryWord.Length >= 7
                    ? 2
                    : 1;


            var distance =
                LevenshteinDistance(
                    queryWord,
                    candidate);


            if (distance <= maxDistance)
            {
                return true;
            }
        }


        return false;
    }


    // =========================================================
    // SŁOWA
    // =========================================================

    private static IEnumerable<string> GetWords(
        string text)
    {
        return text
            .Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);
    }


    // =========================================================
    // NORMALIZACJA
    // =========================================================

    private static string Normalize(
        string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }


        var normalized =
            text
                .ToLowerInvariant()
                .Normalize(
                    NormalizationForm.FormD);


        var builder =
            new StringBuilder();


        foreach (var character in normalized)
        {
            var category =
                CharUnicodeInfo.GetUnicodeCategory(
                    character);


            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }


        var result =
            builder
                .ToString()
                .Normalize(
                    NormalizationForm.FormC);


        result =
            Regex.Replace(
                result,
                @"[^\p{L}\p{N}]+",
                " ");


        return Regex.Replace(
                result,
                @"\s+",
                " ")
            .Trim();
    }


    // =========================================================
    // LEVENSHTEIN
    // =========================================================

    private static int LevenshteinDistance(
        string a,
        string b)
    {
        if (string.IsNullOrEmpty(a))
        {
            return b.Length;
        }

        if (string.IsNullOrEmpty(b))
        {
            return a.Length;
        }


        var matrix =
            new int[
                a.Length + 1,
                b.Length + 1];


        for (var i = 0; i <= a.Length; i++)
        {
            matrix[i, 0] = i;
        }


        for (var j = 0; j <= b.Length; j++)
        {
            matrix[0, j] = j;
        }


        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var cost =
                    a[i - 1] == b[j - 1]
                        ? 0
                        : 1;


                matrix[i, j] =
                    Math.Min(
                        Math.Min(
                            matrix[i - 1, j] + 1,
                            matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
            }
        }


        return matrix[
            a.Length,
            b.Length];
    }


    // =========================================================
    // WYNIK
    // =========================================================

    private sealed record SearchResult(
        Ad Ad,
        int Score);
}

