namespace DontAbandonYourFriends.Services;

internal sealed class RunSavePreview
{
    public int ActDisplay { get; init; }
    public int? Floor { get; init; }
    public int Ascension { get; init; }

    /// <summary>Raw <c>save_time</c> from <see cref="MegaCrit.Sts2.Core.Saves.SerializableRun"/> (unix seconds or ms); for sorting, not shown in UI.</summary>
    public long SaveTime { get; init; }
    public string? RunStartedLine { get; init; }
    public string? LastPlayedLine { get; init; }
    public string? RunDurationLine { get; init; }
    public IReadOnlyList<PlayerRowPreview> Players { get; init; } = Array.Empty<PlayerRowPreview>();
}

internal sealed class PlayerRowPreview
{
    public ulong NetId { get; init; }

    /// <summary>
    /// 1-based index in the save's players array. Used for display when we don't have a persona name.
    /// </summary>
    public int PlayerIndex { get; init; }

    /// <summary>From sidecar metadata (Steam persona) when available.</summary>
    public string? DisplayName { get; init; }

    public string CharacterIdEntry { get; init; } = "?";
    public int CurrentHp { get; init; }
    public int MaxHp { get; init; }
    public int DeckCount { get; init; }
    public int RelicCount { get; init; }
}
