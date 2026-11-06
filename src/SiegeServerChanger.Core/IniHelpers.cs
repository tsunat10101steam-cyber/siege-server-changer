namespace SiegeServerChanger.Core;

internal static class IniHelpers
{
    public static bool TryGetSection(string trimmedLine, out string section)
    {
        if (trimmedLine.Length >= 2 && trimmedLine[0] == '[' && trimmedLine[^1] == ']')
        {
            section = trimmedLine[1..^1].Trim();
            return true;
        }
        section = "";
        return false;
    }
}
