using System.Globalization;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Runs;

namespace DontAbandonYourFriends.Services;

internal static class RunSavePreviewFactory
{
    public static RunSavePreview Build(string json, MultiplayerSaveMetaDocument? meta = null)
    {
        try
        {
            ReadSaveResult<SerializableRun> result = SaveManager.FromJson<SerializableRun>(json);
            if (!result.Success || result.SaveData == null)
            {
                return EmptyPreview();
            }

            SerializableRun run = result.SaveData;
            int actDisplay = run.CurrentActIndex + 1;
            int? floor = null;

            try
            {
                RunState rs = RunState.FromSerializable(run);
                actDisplay = rs.CurrentActIndex + 1;
                // ActFloor is often 0 between nodes; TotalFloor matches cumulative map progress (see RunState.TotalFloor).
                int totalFloors = rs.TotalFloor;
                floor = totalFloors > 0 ? totalFloors : (rs.ActFloor > 0 ? rs.ActFloor : TryReadFloorFromJson(json));
            }
            catch (Exception ex)
            {
                GD.PrintErr($"[DontAbandonYourFriends] RunState.FromSerializable failed (preview fallback): {ex.Message}");
                floor = TryReadFloorFromJson(json);
            }

            Dictionary<ulong, string?>? namesByNet = null;
            if (meta?.Players is { Count: > 0 })
            {
                namesByNet = new Dictionary<ulong, string?>();
                foreach (MetaPlayerEntry mp in meta.Players)
                {
                    namesByNet[mp.NetId] = mp.DisplayName;
                }
            }

            var players = new List<PlayerRowPreview>();
            if (run.Players is { Count: > 0 })
            {
                int idx = 0;
                foreach (SerializablePlayer p in run.Players)
                {
                    idx++;
                    string idEntry = "?";
                    try
                    {
                        if (p.CharacterId is { } cid)
                        {
                            CharacterModel? ch = ModelDb.GetByIdOrNull<CharacterModel>(cid);
                            idEntry = ch?.Id.Entry ?? cid.Entry;
                        }
                    }
                    catch (Exception ex)
                    {
                        GD.PrintErr($"[DontAbandonYourFriends] Character lookup failed: {ex.Message}");
                        if (p.CharacterId is { } cid2)
                        {
                            idEntry = cid2.Entry;
                        }
                    }

                    string? displayName = null;
                    if (namesByNet != null && namesByNet.TryGetValue(p.NetId, out string? dn))
                    {
                        displayName = SanitizePersonaName(dn, p.NetId);
                    }

                    players.Add(new PlayerRowPreview
                    {
                        NetId = p.NetId,
                        PlayerIndex = idx,
                        DisplayName = displayName,
                        CharacterIdEntry = idEntry,
                        CurrentHp = p.CurrentHp,
                        MaxHp = p.MaxHp,
                        DeckCount = p.Deck?.Count ?? 0,
                        RelicCount = p.Relics?.Count ?? 0,
                    });
                }
            }

            return new RunSavePreview
            {
                ActDisplay = actDisplay,
                Floor = floor,
                Ascension = run.Ascension,
                SaveTime = run.SaveTime,
                RunStartedLine = FormatStartTime(run.StartTime),
                LastPlayedLine = FormatLastPlayed(run.SaveTime),
                RunDurationLine = FormatRunDuration(run.RunTime),
                Players = players,
            };
        }
        catch (Exception ex)
        {
            GD.PrintErr($"[DontAbandonYourFriends] RunSavePreviewFactory.Build failed: {ex.Message}");
            return EmptyPreview();
        }
    }

    private static string? FormatStartTime(long unixish)
    {
        DateTimeOffset? dto = TryParseUnixish(unixish);
        if (dto == null)
        {
            return null;
        }

        return "Started: " + dto.Value.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);
    }

    private static string? FormatLastPlayed(long unixish)
    {
        DateTimeOffset? dto = TryParseUnixish(unixish);
        if (dto == null)
        {
            return null;
        }

        DateTime lastLocalDay = dto.Value.ToLocalTime().Date;
        int days = (DateTime.Today - lastLocalDay).Days;
        if (days < 0)
        {
            days = 0;
        }

        if (days == 0)
        {
            return "Last played: today";
        }

        if (days == 1)
        {
            return "Last played: yesterday";
        }

        return $"Last played: {days} days ago";
    }

    private static string? FormatRunDuration(long runTimeRaw)
    {
        if (runTimeRaw <= 0)
        {
            return null;
        }

        TimeSpan span;
        if (runTimeRaw > 10_000_000L)
        {
            span = TimeSpan.FromMilliseconds(runTimeRaw);
        }
        else
        {
            span = TimeSpan.FromSeconds(runTimeRaw);
        }

        if (span.TotalDays >= 1d)
        {
            return $"Run time: {(int)span.TotalDays}d {span.Hours}h {span.Minutes}m";
        }

        if (span.TotalHours >= 1d)
        {
            return $"Run time: {(int)span.TotalHours}h {span.Minutes}m";
        }

        return $"Run time: {(int)span.TotalMinutes}m {span.Seconds}s";
    }

    private static DateTimeOffset? TryParseUnixish(long value)
    {
        if (value <= 0)
        {
            return null;
        }

        try
        {
            if (value > 10_000_000_000L)
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(value);
            }

            return DateTimeOffset.FromUnixTimeSeconds(value);
        }
        catch
        {
            return null;
        }
    }

    private static int? TryReadFloorFromJson(string json)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("map_point_history", out JsonElement hist) &&
                hist.ValueKind == JsonValueKind.Array)
            {
                // Mirror RunState.TotalFloor: sum entries across all acts.
                int total = 0;
                foreach (JsonElement actArr in hist.EnumerateArray())
                {
                    if (actArr.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    foreach (JsonElement _ in actArr.EnumerateArray())
                    {
                        total++;
                    }
                }

                return total > 0 ? total : null;
            }
        }
        catch
        {
            // ignore
        }

        return null;
    }

    private static string? SanitizePersonaName(string? maybeName, ulong netId)
    {
        if (string.IsNullOrWhiteSpace(maybeName))
        {
            return null;
        }

        string s = maybeName.Trim();
        if (s.Length == 0)
        {
            return null;
        }

        // Some Steam/EOS fallbacks can store numeric IDs as "names".
        // Never show those as player labels; fall back to Player N instead.
        if (s == netId.ToString())
        {
            return null;
        }

        bool allDigits = true;
        foreach (char c in s)
        {
            if (!char.IsDigit(c))
            {
                allDigits = false;
                break;
            }
        }

        if (allDigits)
        {
            return null;
        }

        return s;
    }

    internal static RunSavePreview EmptyPreview()
    {
        return new RunSavePreview
        {
            ActDisplay = 1,
            Floor = null,
            Ascension = 0,
            SaveTime = 0,
            RunStartedLine = null,
            LastPlayedLine = null,
            RunDurationLine = null,
            Players = Array.Empty<PlayerRowPreview>(),
        };
    }
}
