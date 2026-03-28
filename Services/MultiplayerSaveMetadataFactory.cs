using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace DontAbandonYourFriends.Services;

internal static class MultiplayerSaveMetadataFactory
{
    /// <summary>Builds a sidecar snapshot from run JSON, optionally merging Steam persona names from a previous document when offline.</summary>
    public static MultiplayerSaveMetaDocument BuildFromRunJson(string runJson, MultiplayerSaveMetaDocument? previousMeta)
    {
        var doc = new MultiplayerSaveMetaDocument
        {
            SchemaVersion = 1,
            CapturedUtc = DateTime.UtcNow.ToString("O"),
            Players = new List<MetaPlayerEntry>(),
        };

        Dictionary<ulong, string?>? prevByNet = null;
        if (previousMeta?.Players is { Count: > 0 })
        {
            prevByNet = new Dictionary<ulong, string?>();
            foreach (MetaPlayerEntry p in previousMeta.Players)
            {
                prevByNet[p.NetId] = p.DisplayName;
            }
        }

        ReadSaveResult<SerializableRun> result = SaveManager.FromJson<SerializableRun>(runJson);
        if (!result.Success || result.SaveData == null)
        {
            return doc;
        }

        SerializableRun run = result.SaveData;

        if (run.Players is not { Count: > 0 } players)
        {
            return doc;
        }

        foreach (SerializablePlayer p in players)
        {
            string? name = SteamPersonaNameResolver.TryGetPersonaName(p.NetId);
            if (string.IsNullOrWhiteSpace(name) && prevByNet != null && prevByNet.TryGetValue(p.NetId, out string? prevName) &&
                !string.IsNullOrWhiteSpace(prevName))
            {
                name = prevName;
            }

            doc.Players.Add(new MetaPlayerEntry { NetId = p.NetId, DisplayName = name });
        }

        return doc;
    }
}
