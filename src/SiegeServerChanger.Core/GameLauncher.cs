using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace SiegeServerChanger.Core;

public enum LauncherKind { Steam, Ubisoft }

public static class GameLauncher
{
    public const string SteamAppId = "359550";
    public const string UbisoftGameId = "635";

    public static string GetUri(LauncherKind kind) => kind switch
    {
        LauncherKind.Steam => $"steam://rungameid/{SteamAppId}",
        LauncherKind.Ubisoft => $"uplay://launch/{UbisoftGameId}/0",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static string GetName(LauncherKind kind) => kind switch
    {
        LauncherKind.Steam => "Steam",
        LauncherKind.Ubisoft => "Ubisoft Connect",
        _ => kind.ToString(),
    };

    /// <summary>Launchers on which Siege is installed, Steam first.</summary>
    [SupportedOSPlatform("windows")]
    public static IReadOnlyList<LauncherKind> DetectInstalled()
    {
        var found = new List<LauncherKind>();
        try
        {
            using var steam = Registry.CurrentUser.OpenSubKey($@"Software\Valve\Steam\Apps\{SteamAppId}");
            if (steam?.GetValue("Installed") is int i && i == 1) found.Add(LauncherKind.Steam);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException) { }

        try
        {
            using var ubi = Registry.LocalMachine.OpenSubKey($@"SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\{UbisoftGameId}");
            if (ubi?.GetValue("InstallDir") is string dir && Directory.Exists(dir)) found.Add(LauncherKind.Ubisoft);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException) { }

        return found;
    }

    public static void Launch(LauncherKind kind) =>
        Process.Start(new ProcessStartInfo(GetUri(kind)) { UseShellExecute = true })?.Dispose();
}
