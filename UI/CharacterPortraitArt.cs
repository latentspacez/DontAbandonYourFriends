using System;
using System.Collections.Generic;

namespace DontAbandonYourFriends.UI;

/// <summary>Maps StS2 character ids from save JSON to optional portrait asset filenames.</summary>
internal static class CharacterPortraitArt
{
    public static IReadOnlyList<string> GetPortraitCandidates(string? characterSlugOrClass)
    {
        var stems = new List<string>();

        string title = FormatCharacterTitle(characterSlugOrClass);
        if (title != "-")
        {
            stems.Add(TitleToFileStem(title));
        }

        string raw = characterSlugOrClass?.Trim() ?? "";
        if (raw.Length > 0)
        {
            stems.Add(RawClassToFileStem(raw));
        }

        if (stems.Count == 0)
        {
            return DefaultUnknownPortrait();
        }

        var files = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string stem in stems)
        {
            if (string.IsNullOrWhiteSpace(stem))
            {
                continue;
            }

            foreach (string name in StemToFileNames(stem))
            {
                if (seen.Add(name))
                {
                    files.Add(name);
                }
            }
        }

        return files.Count > 0 ? files : DefaultUnknownPortrait();
    }

    public static string FormatCharacterTitle(string? characterSlugOrClass)
    {
        string s = characterSlugOrClass?.Trim() ?? "";
        if (string.IsNullOrEmpty(s))
        {
            return "-";
        }

        string norm = NormalizeForMatch(s);

        if (Contains(norm, "DEFECT"))
        {
            return "The Defect";
        }

        if (Contains(norm, "IRONCLAD"))
        {
            return "The Ironclad";
        }

        if (Contains(norm, "SILENT") || Contains(norm, "GREEN"))
        {
            return "The Silent";
        }

        if (Contains(norm, "NECROBINDER") || Contains(norm, "NECRO"))
        {
            return "The Necrobinder";
        }

        if (Contains(norm, "REGENT"))
        {
            return "The Regent";
        }

        return HumanizeUnknownClass(s);
    }

    private static string TitleToFileStem(string title) =>
        title.Replace(" ", "", StringComparison.Ordinal).Replace("'", "", StringComparison.Ordinal);

    private static string RawClassToFileStem(string raw)
    {
        string compact = raw.Replace(" ", "", StringComparison.Ordinal)
            .Replace("'", "", StringComparison.Ordinal)
            .Replace("_", "", StringComparison.Ordinal);

        if (compact.StartsWith("The", StringComparison.OrdinalIgnoreCase)
            && compact.Length > 3
            && char.IsUpper(compact[3]))
        {
            return compact;
        }

        string norm = compact.ToUpperInvariant();
        if (Contains(norm, "DEFECT"))
        {
            return "TheDefect";
        }

        if (Contains(norm, "IRONCLAD"))
        {
            return "TheIronclad";
        }

        if (Contains(norm, "SILENT") || Contains(norm, "GREEN"))
        {
            return "TheSilent";
        }

        if (Contains(norm, "NECROBINDER") || Contains(norm, "NECRO"))
        {
            return "TheNecrobinder";
        }

        if (Contains(norm, "REGENT"))
        {
            return "TheRegent";
        }

        return compact;
    }

    private static IEnumerable<string> StemToFileNames(string stem)
    {
        yield return stem + ".PNG";
        yield return stem + ".png";
    }

    private static string HumanizeUnknownClass(string raw)
    {
        if (raw.Length <= 1)
        {
            return raw;
        }

        if (raw.Contains(' ', StringComparison.Ordinal) || raw.Contains('_', StringComparison.Ordinal))
        {
            return raw.Replace("_", " ", StringComparison.Ordinal);
        }

        return raw;
    }

    private static string[] DefaultUnknownPortrait() =>
        ["TheIronclad.PNG", "TheIronclad.png"];

    private static string NormalizeForMatch(string s) =>
        s.Replace(" ", "", StringComparison.Ordinal).Replace("_", "", StringComparison.Ordinal).ToUpperInvariant();

    private static bool Contains(string norm, string token) => norm.Contains(token, StringComparison.Ordinal);
}
