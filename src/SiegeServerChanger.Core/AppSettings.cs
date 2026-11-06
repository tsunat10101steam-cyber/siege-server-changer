using System.Text.Json;

namespace SiegeServerChanger.Core;

/// <summary>Per-user preferences of this tool (not the game's settings).</summary>
public sealed class AppSettings
{
    public const int MaxRecent = 5;

    public List<string> Favorites { get; set; } = [];
    public List<string> Recent { get; set; } = [];
    public LauncherKind? Launcher { get; set; }

    public bool IsFavorite(string id) => Favorites.Contains(id, StringComparer.OrdinalIgnoreCase);

    /// <summary>Adds or removes; returns the new state.</summary>
    public bool ToggleFavorite(string id)
    {
        if (Favorites.RemoveAll(f => f.Equals(id, StringComparison.OrdinalIgnoreCase)) > 0) return false;
        Favorites.Add(id);
        return true;
    }

    /// <summary>Most recent first, no duplicates, capped at MaxRecent.</summary>
    public void AddRecent(string id)
    {
        Recent.RemoveAll(r => r.Equals(id, StringComparison.OrdinalIgnoreCase));
        Recent.Insert(0, id);
        if (Recent.Count > MaxRecent) Recent.RemoveRange(MaxRecent, Recent.Count - MaxRecent);
    }

    /// <summary>"default" first, then favorites (in the order they were starred), then the rest in catalog order.</summary>
    public IReadOnlyList<DataCenter> Order(IEnumerable<DataCenter> servers)
    {
        var list = servers.ToList();
        int Rank(DataCenter d)
        {
            if (d.Id.Equals(DataCenter.DefaultId, StringComparison.OrdinalIgnoreCase)) return -1;
            var i = Favorites.FindIndex(f => f.Equals(d.Id, StringComparison.OrdinalIgnoreCase));
            return i >= 0 ? i : int.MaxValue;
        }
        // OrderBy is stable, so non-favorites keep catalog order.
        return list.OrderBy(Rank).ToList();
    }
}

public static class AppSettingsStore
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SiegeServerChanger", "settings.json");

    /// <summary>Missing or corrupt file gives fresh settings instead of an error.</summary>
    public static AppSettings Load(string? path = null)
    {
        path ??= DefaultPath;
        try
        {
            if (!File.Exists(path)) return new AppSettings();
            var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), Json) ?? new AppSettings();
            s.Favorites ??= [];
            s.Recent ??= [];
            return s;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings, string? path = null)
    {
        path ??= DefaultPath;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Json));
        File.Move(tmp, path, overwrite: true);
    }
}
