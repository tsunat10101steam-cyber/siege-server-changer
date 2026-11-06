using System.Text.RegularExpressions;

namespace SiegeServerChanger.Core;

/// <summary>
/// Builds the list of selectable servers. The game writes the valid values as a
/// comment block under [ONLINE] (";DataCenterHint =>" followed by ";    value" lines),
/// so we read them from the file and only fall back to a built-in list if that fails.
/// </summary>
public static partial class DataCenterCatalog
{
    private static readonly Dictionary<string, string> KnownNames = new(StringComparer.OrdinalIgnoreCase)
    {
        [DataCenter.DefaultId] = "자동 (핑 기준)",
        ["gamelift/ap-northeast-2"] = "서울",
        ["gamelift/ap-northeast-1"] = "도쿄",
        ["gamelift/ap-northeast-3"] = "오사카",
        ["gamelift/ap-east-1"] = "홍콩",
        ["gamelift/ap-southeast-1"] = "싱가포르",
        ["gamelift/ap-southeast-2"] = "시드니",
        ["gamelift/ap-south-1"] = "뭄바이",
        ["gamelift/eu-north-1"] = "스톡홀름",
        ["gamelift/eu-west-1"] = "아일랜드",
        ["gamelift/eu-west-2"] = "런던",
        ["gamelift/eu-west-3"] = "파리",
        ["gamelift/eu-central-1"] = "프랑크푸르트",
        ["gamelift/eu-south-1"] = "밀라노",
        ["gamelift/af-south-1"] = "케이프타운",
        ["gamelift/sa-east-1"] = "상파울루",
        ["gamelift/us-east-1"] = "미국 버지니아",
        ["gamelift/us-east-2"] = "미국 오하이오",
        ["gamelift/us-west-1"] = "미국 캘리포니아",
        ["gamelift/us-west-2"] = "미국 오리건",
        ["gamelift/ca-central-1"] = "캐나다 중부",
        ["playfab/uaenorth"] = "UAE 북부",
    };

    [GeneratedRegex(@"^;\s+([A-Za-z0-9_\-]+(?:/[A-Za-z0-9_\-]+)?)(?:\s.*)?$")]
    private static partial Regex ValueLine();

    public static string GetDisplayName(string id) =>
        KnownNames.TryGetValue(id, out var name) ? name : id;

    public static DataCenter Create(string id) => new(id, GetDisplayName(id));

    /// <summary>Built-in list used when the file has no readable comment block.</summary>
    public static IReadOnlyList<DataCenter> Fallback { get; } =
        KnownNames.Keys.Select(Create).ToList();

    /// <summary>Parses the value list from the file's comment block. Empty if not found.</summary>
    public static IReadOnlyList<DataCenter> ParseFromLines(IReadOnlyList<string> lines)
    {
        var result = new List<DataCenter>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? section = null;

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].Trim();
            if (IniHelpers.TryGetSection(trimmed, out var s)) { section = s; continue; }
            if (!string.Equals(section, GameSettingsFile.OnlineSection, StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsListHeader(trimmed)) continue;

            for (int j = i + 1; j < lines.Count; j++)
            {
                var m = ValueLine().Match(lines[j].TrimEnd());
                if (!m.Success) break;
                var id = m.Groups[1].Value;
                if (seen.Add(id)) result.Add(Create(id));
            }
            break;
        }
        return result;
    }

    /// <summary>File list if available, otherwise the fallback. "default" is always first.</summary>
    public static IReadOnlyList<DataCenter> Load(IReadOnlyList<string> lines)
    {
        var parsed = ParseFromLines(lines);
        var list = (parsed.Count > 0 ? parsed : Fallback).ToList();
        var def = list.FindIndex(d => d.Id.Equals(DataCenter.DefaultId, StringComparison.OrdinalIgnoreCase));
        if (def < 0) list.Insert(0, Create(DataCenter.DefaultId));
        else if (def > 0) { var d = list[def]; list.RemoveAt(def); list.Insert(0, d); }
        return list;
    }

    private static bool IsListHeader(string trimmed)
    {
        if (!trimmed.StartsWith(';')) return false;
        var body = trimmed[1..].Trim();
        return body.StartsWith(GameSettingsFile.HintKey, StringComparison.OrdinalIgnoreCase) && body.Contains("=>");
    }
}
