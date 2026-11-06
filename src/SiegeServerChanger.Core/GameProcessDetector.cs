using System.Diagnostics;

namespace SiegeServerChanger.Core;

public static class GameProcessDetector
{
    private static readonly string[] ProcessNames = ["RainbowSix", "RainbowSix_BE", "RainbowSix_Vulkan"];

    /// <summary>The game rewrites GameSettings.ini on exit, so edits made while it runs can be lost.</summary>
    public static bool IsGameRunning()
    {
        foreach (var name in ProcessNames)
        {
            var procs = Process.GetProcessesByName(name);
            var running = procs.Length > 0;
            foreach (var p in procs) p.Dispose();
            if (running) return true;
        }
        return false;
    }
}
