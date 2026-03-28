using System.Reflection;

namespace DontAbandonYourFriends;

internal static class ModVersionInfo
{
    public static string GetInformationalVersion()
    {
        try
        {
            var asm = typeof(ModVersionInfo).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            if (!string.IsNullOrWhiteSpace(info?.InformationalVersion))
            {
                return info.InformationalVersion;
            }

            return asm.GetName().Version?.ToString() ?? "unknown";
        }
        catch
        {
            return "unknown";
        }
    }
}
