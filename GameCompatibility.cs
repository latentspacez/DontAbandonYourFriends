using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace DontAbandonYourFriends;

internal static class GameCompatibility
{
    private const string GateTag = "[release_info gate] ";

    public static bool IsSupportedGameBuild(out string detailMessage)
    {
        detailMessage = "";
        try
        {
            if (!TryGetSts2AssemblyPath(out string? sts2Path, out string? resolveError) || string.IsNullOrEmpty(sts2Path))
            {
                detailMessage = GateTag + (resolveError ?? "Could not resolve sts2 assembly path.");
                return false;
            }

            if (!TryReadReleaseInfoVersion(sts2Path, out string? releaseVersion, out string? releaseInfoPath, out string? readError))
            {
                detailMessage =
                    GateTag +
                    $"Could not read game version from release_info.json (expected next to the game install). {readError ?? ""} " +
                    $"sts2.dll path: '{sts2Path}'.";
                return false;
            }

            if (!GameCompatibilityConstants.IsSupportedGameVersionLabel(releaseVersion!))
            {
                detailMessage =
                    GateTag +
                    $"release_info.json at '{releaseInfoPath}' reports Version={releaseVersion}, which is not in this mod's supported list: " +
                    $"{string.Join(", ", GameCompatibilityConstants.SupportedGameDisplayLabels)}.";
                return false;
            }

            string? productVersion = null;
            try
            {
                productVersion = FileVersionInfo.GetVersionInfo(sts2Path).ProductVersion;
            }
            catch
            {
                /* optional diagnostics */
            }

            detailMessage =
                GateTag +
                $"OK: game version from release_info.json (same source as MCP): {releaseVersion} " +
                $"(file: '{releaseInfoPath}'), sts2.dll: '{sts2Path}'" +
                (productVersion != null ? $", ProductVersion={productVersion}" : "") +
                $"; supported: {string.Join(", ", GameCompatibilityConstants.SupportedGameDisplayLabels)}.";
            return true;
        }
        catch (Exception ex)
        {
            detailMessage = GateTag + $"Game compatibility check failed: {ex.Message}";
            return false;
        }
    }

    public static bool TryGetSts2AssemblyPath(out string? assemblyPath, out string? error)
    {
        assemblyPath = null;
        error = null;

        try
        {
            var asm = typeof(RunManager).Assembly;
            string? path = asm.Location;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "RunManager assembly has no Location; cannot locate sts2.dll.";
                return false;
            }

            assemblyPath = path;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Reads <c>release_info.json</c> in the game install root (parent of <c>data_sts2_*</c>), where
    /// <c>version</c> matches MCP / Steam (e.g. <c>v0.99.1</c>). This is not the same as <c>sts2.dll</c> FileVersion.
    /// </summary>
    public static bool TryReadReleaseInfoVersion(
        string sts2DllPath,
        out string? versionLabel,
        out string? releaseInfoPath,
        out string? error)
    {
        versionLabel = null;
        releaseInfoPath = null;
        error = null;

        try
        {
            string? dataDir = Path.GetDirectoryName(sts2DllPath);
            if (string.IsNullOrEmpty(dataDir))
            {
                error = "Could not get directory of sts2.dll.";
                return false;
            }

            string? gameRoot = Directory.GetParent(dataDir)?.FullName;
            if (string.IsNullOrEmpty(gameRoot))
            {
                error = "Could not resolve game install root (parent of data_sts2_*).";
                return false;
            }

            releaseInfoPath = Path.Combine(gameRoot, "release_info.json");
            if (!File.Exists(releaseInfoPath))
            {
                error = $"File not found: '{releaseInfoPath}'.";
                return false;
            }

            string json = File.ReadAllText(releaseInfoPath);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("version", out var vEl))
            {
                error = "JSON has no 'version' property.";
                return false;
            }

            string? v = vEl.GetString();
            if (string.IsNullOrWhiteSpace(v))
            {
                error = "'version' is empty.";
                return false;
            }

            versionLabel = v.Trim();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Legacy helper for diagnostics; FileVersion is assembly metadata, not the marketing game version.</summary>
    public static bool TryGetSts2AssemblyVersionInfo(
        out Version? fileVersion,
        out string? productVersion,
        out string? assemblyPath,
        out string? error)
    {
        fileVersion = null;
        productVersion = null;
        assemblyPath = null;
        error = null;

        try
        {
            if (!TryGetSts2AssemblyPath(out string? path, out error))
            {
                return false;
            }

            assemblyPath = path;

            var fvi = FileVersionInfo.GetVersionInfo(path!);
            productVersion = fvi.ProductVersion;

            string? fv = fvi.FileVersion;
            if (string.IsNullOrWhiteSpace(fv))
            {
                error = "sts2.dll FileVersion is empty.";
                return false;
            }

            try
            {
                fileVersion = new Version(fv.Trim());
            }
            catch (Exception ex)
            {
                error = $"Could not parse sts2.dll FileVersion '{fv}': {ex.Message}";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    public static string GetGodotVersionSummary()
    {
        try
        {
            var d = Engine.GetVersionInfo();
            return $"{d["major"]}.{d["minor"]}.{d["patch"]} ({d["status"]})";
        }
        catch (Exception ex)
        {
            return $"unavailable ({ex.Message})";
        }
    }
}
