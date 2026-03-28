using System.Text.Json;
using System.Text.Json.Serialization;

namespace DontAbandonYourFriends.Services;

/// <summary>Sidecar JSON next to each <c>*.save</c> and <c>current_run_mp.meta.json</c> for the live slot.</summary>
internal sealed class MultiplayerSaveMetaDocument
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = 1;

    [JsonPropertyName("capturedUtc")]
    public string CapturedUtc { get; set; } = "";

    [JsonPropertyName("players")]
    public List<MetaPlayerEntry> Players { get; set; } = new();

    /// <summary>Reserved for forward-compatible custom keys (ignored by current mod).</summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}

internal sealed class MetaPlayerEntry
{
    [JsonPropertyName("netId")]
    public ulong NetId { get; set; }

    /// <summary>Steam/EOS display name when resolved; may be null if offline or unknown.</summary>
    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }
}
