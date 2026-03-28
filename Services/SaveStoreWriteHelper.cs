using System;
using System.Text;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Saves;

namespace DontAbandonYourFriends.Services;

/// <summary>
/// Wraps <see cref="ISaveStore.WriteFileAsync"/> so failures when Steam Cloud is off, unreachable, or throws
/// unexpected errors do not break the mod: we prefer the same behavior as the game (local write first), and
/// recover when the combined store throws after local persistence.
/// </summary>
internal static class SaveStoreWriteHelper
{
    private const string LogTag = "[DontAbandonYourFriends]";

    public static async Task<string?> WriteUtf8Async(ISaveStore store, string path, string content)
    {
        return await WriteBytesAsync(store, path, Encoding.UTF8.GetBytes(content));
    }

    public static async Task<string?> WriteBytesAsync(ISaveStore store, string path, byte[] bytes)
    {
        try
        {
            await store.WriteFileAsync(path, bytes);
            return null;
        }
        catch (Exception ex)
        {
            GD.PrintErr($"{LogTag} WriteFileAsync failed ({path}): {ex.Message}");
            return await TryRecoverAfterWriteFailureAsync(store, path, bytes, ex);
        }
    }

    private static async Task<string?> TryRecoverAfterWriteFailureAsync(ISaveStore store, string path, byte[] bytes, Exception original)
    {
        if (store is CloudSaveStore cloud)
        {
            try
            {
                if (cloud.LocalStore.FileExists(path))
                {
                    int size = cloud.LocalStore.GetFileSize(path);
                    if (size == bytes.Length)
                    {
                        GD.PrintErr($"{LogTag} Local file matches expected size after error; treating as success (cloud sync may be off).");
                        return null;
                    }
                }
            }
            catch (Exception probeEx)
            {
                GD.PrintErr($"{LogTag} Could not verify local file: {probeEx.Message}");
            }

            try
            {
                await cloud.LocalStore.WriteFileAsync(path, bytes);
                GD.Print($"{LogTag} Saved via local store only (cloud unavailable or failed).");
                return null;
            }
            catch (Exception ex2)
            {
                GD.PrintErr($"{LogTag} Local-only write failed: {ex2.Message}");
                return ex2.Message;
            }
        }

        return original.Message;
    }
}
