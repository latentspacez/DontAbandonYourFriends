using System.Text.Json;

namespace DontAbandonYourFriends.Services;

internal static class DontAbandonJson
{
    public static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
}
