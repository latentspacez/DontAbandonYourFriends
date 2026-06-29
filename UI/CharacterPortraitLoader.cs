using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;

namespace DontAbandonYourFriends.UI;

/// <summary>Loads optional DAyF character portraits, falling back to native StS2 character icons.</summary>
internal static class CharacterPortraitLoader
{
    private const string EmbeddedPrefix = "DontAbandonYourFriends.embedded.characters.";
    private static readonly Dictionary<string, Texture2D?> TextureCache = new(StringComparer.Ordinal);
    private static Dictionary<string, string>? _embeddedResourceNames;

    public static Texture2D? TryLoadPlayerTexture(string? characterEntry)
    {
        string id = characterEntry?.Trim() ?? "";
        if (id.Length > 0)
        {
            Texture2D? portrait = TryLoadPortraitTexture(CharacterPortraitArt.GetPortraitCandidates(id));
            if (portrait != null)
            {
                return portrait;
            }

            Texture2D? icon = TryLoadGameTexture($"ui/top_panel/character_icon_{id.ToLowerInvariant()}.png");
            if (icon != null)
            {
                return icon;
            }
        }

        return TryLoadGameTexture("packed/common_ui/locked_model.png")
               ?? TryLoadGameTexture("packed/run_history/power_portrait.png");
    }

    private static Texture2D? TryLoadPortraitTexture(IReadOnlyList<string> fileNameCandidates)
    {
        if (fileNameCandidates.Count == 0)
        {
            return null;
        }

        List<string> namesToTry = fileNameCandidates
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        string cacheKey = string.Join("|", namesToTry);
        if (TextureCache.TryGetValue(cacheKey, out Texture2D? cached)
            && (cached == null || GodotObject.IsInstanceValid(cached)))
        {
            return cached;
        }

        foreach (string dir in GetCharacterPortraitSearchDirectories())
        {
            foreach (string name in namesToTry)
            {
                Texture2D? diskTexture = TryLoadDiskTexture(Path.Combine(dir, name));
                if (diskTexture != null)
                {
                    TextureCache[cacheKey] = diskTexture;
                    return diskTexture;
                }
            }
        }

        foreach (string name in namesToTry)
        {
            Texture2D? embedded = TryLoadEmbeddedTexture(name);
            if (embedded != null)
            {
                TextureCache[cacheKey] = embedded;
                return embedded;
            }
        }

        foreach (string name in namesToTry)
        {
            string resPath = $"res://mods/DontAbandonYourFriends/assets/characters/{name}";
            if (ResourceLoader.Exists(resPath))
            {
                Texture2D? texture = ResourceLoader.Load<Texture2D>(resPath);
                TextureCache[cacheKey] = texture;
                return texture;
            }
        }

        TextureCache[cacheKey] = null;
        return null;
    }

    private static IEnumerable<string> GetCharacterPortraitSearchDirectories()
    {
        string? dllDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrWhiteSpace(dllDir))
        {
            yield return Path.Combine(dllDir, "assets", "characters");
            yield return Path.Combine(dllDir, "..", "assets", "characters");
        }

        string? devRoot = System.Environment.GetEnvironmentVariable("DAYF_DEV_ROOT");
        if (!string.IsNullOrWhiteSpace(devRoot))
        {
            yield return Path.Combine(devRoot.Trim(), "assets", "characters");
        }
    }

    private static Texture2D? TryLoadDiskTexture(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var image = new Image();
            Error err = image.Load(path);
            if (err != Error.Ok)
            {
                byte[] bytes = File.ReadAllBytes(path);
                err = image.LoadPngFromBuffer(bytes);
                if (err != Error.Ok)
                {
                    err = image.LoadJpgFromBuffer(bytes);
                }
            }

            return err == Error.Ok ? ImageTexture.CreateFromImage(image) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Texture2D? TryLoadEmbeddedTexture(string fileName)
    {
        try
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            string requestedName = EmbeddedPrefix + fileName.Trim();
            if (!GetEmbeddedResourceNames(assembly).TryGetValue(requestedName, out string? canonicalName))
            {
                return null;
            }

            using Stream? stream = assembly.GetManifestResourceStream(canonicalName);
            if (stream == null)
            {
                return null;
            }

            using var memory = new MemoryStream();
            stream.CopyTo(memory);

            var image = new Image();
            Error err = image.LoadPngFromBuffer(memory.ToArray());
            if (err != Error.Ok)
            {
                err = image.LoadJpgFromBuffer(memory.ToArray());
            }

            return err == Error.Ok ? ImageTexture.CreateFromImage(image) : null;
        }
        catch
        {
            return null;
        }
    }

    private static Dictionary<string, string> GetEmbeddedResourceNames(Assembly assembly)
    {
        if (_embeddedResourceNames != null)
        {
            return _embeddedResourceNames;
        }

        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (string name in assembly.GetManifestResourceNames())
            {
                if (name.StartsWith(EmbeddedPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    names[name] = name;
                }
            }
        }
        catch
        {
            // Empty map; the game icon fallback keeps the UI usable.
        }

        _embeddedResourceNames = names;
        return names;
    }

    private static Texture2D? TryLoadGameTexture(string relativePath)
    {
        string path = ImageHelper.GetImagePath(relativePath);
        try
        {
            Texture2D? texture = PreloadManager.Cache.GetTexture2D(path);
            if (texture != null && GodotObject.IsInstanceValid(texture))
            {
                return texture;
            }
        }
        catch
        {
        }

        try
        {
            Texture2D? texture = PreloadManager.Cache.GetCompressedTexture2D(path);
            if (texture != null && GodotObject.IsInstanceValid(texture))
            {
                return texture;
            }
        }
        catch
        {
        }

        return null;
    }
}
