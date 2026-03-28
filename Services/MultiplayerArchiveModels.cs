using System.Text.Json.Serialization;

namespace DontAbandonYourFriends.Services;

internal sealed class ArchiveIndexDocument
{
    [JsonPropertyName("entries")]
    public List<ArchiveIndexEntry> Entries { get; set; } = new();
}

internal sealed class ArchiveIndexEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("storageFileName")]
    public string StorageFileName { get; set; } = "";

    [JsonPropertyName("createdUtc")]
    public string CreatedUtc { get; set; } = "";
}

internal sealed class ArchiveListRow
{
    public required ArchiveIndexEntry Entry { get; init; }
    public required RunSavePreview Preview { get; init; }
}

internal sealed class LiveMultiplayerSlotInfo
{
    public bool HasSave { get; init; }
    public required RunSavePreview Preview { get; init; }
}

internal sealed class ArchiveListResult
{
    public required LiveMultiplayerSlotInfo Live { get; init; }
    public required IReadOnlyList<ArchiveListRow> Archives { get; init; }
}
