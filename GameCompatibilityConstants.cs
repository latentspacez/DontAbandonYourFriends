using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DontAbandonYourFriends;

/// <summary>Loads embedded <c>Sts2SupportedVersions.json</c>; runtime gate uses <c>release_info.json</c> <c>version</c> (MCP-aligned), not <c>sts2.dll</c> FileVersion.</summary>
internal static class GameCompatibilityConstants
{
    private const string EmbeddedResourceName = "DontAbandonYourFriends.Sts2SupportedVersions.json";

    /// <summary>Display labels from <c>Sts2SupportedVersions.json</c> (e.g. v0.99.1), sorted.</summary>
    public static readonly IReadOnlyList<string> SupportedGameDisplayLabels;

    private static readonly HashSet<string> SupportedGameVersionNormalized = new(StringComparer.Ordinal);

    static GameCompatibilityConstants()
    {
        var doc = LoadDocument();
        if (doc.SupportedGameBuilds.Count == 0)
        {
            throw new InvalidOperationException("Sts2SupportedVersions.json: supportedGameBuilds is empty.");
        }

        SupportedGameDisplayLabels = doc.SupportedGameBuilds
            .Select(b => b.DisplayLabel.Trim())
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToArray();

        foreach (var b in doc.SupportedGameBuilds)
        {
            SupportedGameVersionNormalized.Add(NormalizeGameVersionLabel(b.DisplayLabel));
        }
    }

    /// <summary>Whether <paramref name="releaseInfoVersion"/> (from <c>release_info.json</c> <c>version</c>) is explicitly supported.</summary>
    public static bool IsSupportedGameVersionLabel(string releaseInfoVersion)
    {
        return SupportedGameVersionNormalized.Contains(NormalizeGameVersionLabel(releaseInfoVersion));
    }

    /// <summary>Strips a leading <c>v</c> for comparison; trims.</summary>
    public static string NormalizeGameVersionLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return "";
        }

        string s = label.Trim();
        if (s.Length > 0 && (s[0] == 'v' || s[0] == 'V'))
        {
            s = s.Substring(1);
        }

        return s.Trim();
    }

    private static Sts2SupportedVersionsDocument LoadDocument()
    {
        var asm = typeof(GameCompatibilityConstants).Assembly;
        using var stream = asm.GetManifestResourceStream(EmbeddedResourceName);
        if (stream == null)
        {
            throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found. Ensure Sts2SupportedVersions.json is included as EmbeddedResource.");
        }

        using var reader = new StreamReader(stream);
        string json = reader.ReadToEnd();
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var doc = JsonSerializer.Deserialize<Sts2SupportedVersionsDocument>(json, options);
        if (doc?.SupportedGameBuilds == null || doc.SupportedGameBuilds.Count == 0)
        {
            throw new InvalidOperationException("Sts2SupportedVersions.json: missing or invalid supportedGameBuilds.");
        }

        return doc;
    }

    private sealed class Sts2SupportedVersionsDocument
    {
        [JsonPropertyName("supportedGameBuilds")]
        public List<Sts2GameBuildEntry> SupportedGameBuilds { get; set; } = new();
    }

    private sealed class Sts2GameBuildEntry
    {
        [JsonPropertyName("displayLabel")]
        public string DisplayLabel { get; set; } = "";

        [JsonPropertyName("mcpDecompiledFolderName")]
        public string? McpDecompiledFolderName { get; set; }

        [JsonPropertyName("preprocessorSymbol")]
        public string? PreprocessorSymbol { get; set; }
    }
}
