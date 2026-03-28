using System;
using System.Diagnostics;
using Godot;
using MegaCrit.Sts2.Core.Runs;

namespace DontAbandonYourFriends;

internal static class GameCompatibility
{
    public static bool IsSupportedGameBuild(out string detailMessage)
    {
        detailMessage = "";
        try
        {
            if (!TryGetSts2AssemblyVersionInfo(out Version? fileVersion, out string? productVersion, out string? path, out string? resolveError))
            {
                detailMessage = resolveError ?? "Could not resolve sts2 assembly version.";
                return false;
            }

            var min = GameCompatibilityConstants.SupportedSts2AssemblyFileVersionMin;
            var max = GameCompatibilityConstants.SupportedSts2AssemblyFileVersionMax;

            if (fileVersion < min || fileVersion > max)
            {
                detailMessage =
                    $"sts2.dll at '{path}' FileVersion={fileVersion} (ProductVersion={productVersion}) " +
                    $"is outside supported range [{min}, {max}] (inclusive).";
                return false;
            }

            detailMessage =
                $"OK: sts2.dll FileVersion={fileVersion}, ProductVersion={productVersion}, path={path}, range=[{min}, {max}].";
            return true;
        }
        catch (Exception ex)
        {
            detailMessage = $"Game compatibility check failed: {ex.Message}";
            return false;
        }
    }

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
            var asm = typeof(RunManager).Assembly;
            string? path = asm.Location;
            if (string.IsNullOrWhiteSpace(path))
            {
                error = "RunManager assembly has no Location; cannot read sts2.dll FileVersionInfo.";
                return false;
            }

            assemblyPath = path;

            var fvi = FileVersionInfo.GetVersionInfo(path);
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
