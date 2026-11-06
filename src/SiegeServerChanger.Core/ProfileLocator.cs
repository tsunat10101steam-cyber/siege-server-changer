namespace SiegeServerChanger.Core;

public sealed record SiegeProfile(string Id, string SettingsPath)
{
    public override string ToString() => Id;
}

public static class ProfileLocator
{
    public const string SettingsFileName = "GameSettings.ini";

    /// <summary>
    /// Folder that holds one sub-folder per account. Environment.GetFolderPath uses
    /// SHGetKnownFolderPath, so a Documents folder redirected to OneDrive is handled.
    /// </summary>
    public static string GetSiegeRoot() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "My Games", "Rainbow Six - Siege");

    public static IReadOnlyList<SiegeProfile> Find() => Find(GetSiegeRoot());

    /// <summary>Every sub-folder containing GameSettings.ini (skips "Benchmark" etc.).</summary>
    public static IReadOnlyList<SiegeProfile> Find(string root)
    {
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateDirectories(root)
            .Select(dir => new SiegeProfile(Path.GetFileName(dir), Path.Combine(dir, SettingsFileName)))
            .Where(p => File.Exists(p.SettingsPath))
            .OrderByDescending(p => File.GetLastWriteTimeUtc(p.SettingsPath))
            .ToList();
    }
}
