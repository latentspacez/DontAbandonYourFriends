using System.Reflection;
using MegaCrit.Sts2.Core.Saves;

namespace DontAbandonYourFriends.Services;

/// <summary>Resolves <see cref="ISaveStore"/> from <see cref="SaveManager"/> (same store used for run files and Steam cloud).</summary>
internal static class SaveStoreAccessor
{
    public static ISaveStore? TryGetSaveStore()
    {
        try
        {
            var sm = SaveManager.Instance;
            FieldInfo? f = typeof(SaveManager).GetField("_saveStore", BindingFlags.Instance | BindingFlags.NonPublic);
            return f?.GetValue(sm) as ISaveStore;
        }
        catch
        {
            return null;
        }
    }
}
