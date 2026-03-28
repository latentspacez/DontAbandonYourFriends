using Steamworks;

namespace DontAbandonYourFriends.Services;

/// <summary>Best-effort Steam persona lookup for a 64-bit Steam ID (same value as <c>SerializablePlayer.net_id</c> in multiplayer).</summary>
internal static class SteamPersonaNameResolver
{
    public static string? TryGetPersonaName(ulong steamId64)
    {
        if (steamId64 == 0)
        {
            return null;
        }

        try
        {
            var id = new CSteamID(steamId64);
            if (!id.IsValid())
            {
                return null;
            }

            string name = SteamFriends.GetFriendPersonaName(id);
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            SteamFriends.RequestUserInformation(id, false);
            name = SteamFriends.GetFriendPersonaName(id);
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch
        {
            return null;
        }
    }
}
