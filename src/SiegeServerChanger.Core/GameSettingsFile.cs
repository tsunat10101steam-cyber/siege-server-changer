using System.Text;

namespace SiegeServerChanger.Core;

/// <summary>
/// GameSettings.ini wrapper that changes only the DataCenterHint line and keeps
/// everything else (comments, order, encoding, BOM, line endings) byte-for-byte.
/// </summary>
public sealed class GameSettingsFile
{
    public const string OnlineSection = "ONLINE";
    public const string HintKey = "DataCenterHint";

    private readonly List<string> _lines;
    private readonly Encoding _encoding;
    private readonly byte[] _preamble;
    private readonly string _newLine;
    private readonly bool _trailingNewLine;

    public string FilePath { get; }
    public string BackupPath => FilePath + ".bak";
    public IReadOnlyList<string> Lines => _lines;

    private GameSettingsFile(string path, List<string> lines, Encoding encoding, byte[] preamble, string newLine, bool trailingNewLine)
    {
        FilePath = path;
        _lines = lines;
        _encoding = encoding;
        _preamble = preamble;
        _newLine = newLine;
        _trailingNewLine = trailingNewLine;
    }

    public static GameSettingsFile Load(string path) => Parse(path, File.ReadAllBytes(path));

    public static GameSettingsFile Parse(string path, byte[] bytes)
    {
        var (encoding, bomLength) = DetectEncoding(bytes);
        var text = encoding.GetString(bytes, bomLength, bytes.Length - bomLength);
        var newLine = text.Contains("\r\n") ? "\r\n" : "\n";
        var lines = text.Split(newLine).ToList();
        var trailing = lines.Count > 1 && lines[^1].Length == 0;
        if (trailing) lines.RemoveAt(lines.Count - 1);
        return new GameSettingsFile(path, lines, encoding, bytes[..bomLength], newLine, trailing);
    }

    /// <summary>Current hint value, or null if the key is missing.</summary>
    public string? GetDataCenterHint()
    {
        var i = FindHintLine();
        if (i < 0) return null;
        var line = _lines[i];
        return line[(line.IndexOf('=') + 1)..].Trim();
    }

    /// <summary>Replaces the value. Throws if the key is missing so we never guess the format.</summary>
    public void SetDataCenterHint(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Contains('\n') || value.Contains('\r'))
            throw new ArgumentException("잘못된 서버 값입니다.", nameof(value));

        var i = FindHintLine();
        if (i < 0)
            throw new InvalidOperationException($"[{OnlineSection}] 섹션에서 {HintKey} 항목을 찾지 못했습니다. 파일 형식이 바뀌었을 수 있어 수정하지 않았습니다.");

        var line = _lines[i];
        _lines[i] = line[..(line.IndexOf('=') + 1)] + value.Trim();
    }

    public byte[] ToBytes()
    {
        var text = string.Join(_newLine, _lines) + (_trailingNewLine ? _newLine : "");
        var body = _encoding.GetBytes(text);
        var result = new byte[_preamble.Length + body.Length];
        _preamble.CopyTo(result, 0);
        body.CopyTo(result, _preamble.Length);
        return result;
    }

    /// <summary>Writes atomically. Creates a .bak of the original once, before the first change.</summary>
    public void Save()
    {
        if ((File.GetAttributes(FilePath) & FileAttributes.ReadOnly) != 0)
            throw new IOException("설정 파일이 읽기 전용입니다. 파일 속성에서 읽기 전용을 해제해 주세요.");

        if (!File.Exists(BackupPath))
            File.Copy(FilePath, BackupPath);

        var tmp = FilePath + ".tmp";
        File.WriteAllBytes(tmp, ToBytes());
        File.Move(tmp, FilePath, overwrite: true);
    }

    public bool HasBackup => File.Exists(BackupPath);

    public static void RestoreBackup(string filePath)
    {
        var bak = filePath + ".bak";
        if (!File.Exists(bak)) throw new FileNotFoundException("백업 파일이 없습니다.", bak);
        File.Copy(bak, filePath, overwrite: true);
    }

    private int FindHintLine()
    {
        string? section = null;
        for (int i = 0; i < _lines.Count; i++)
        {
            var trimmed = _lines[i].Trim();
            if (IniHelpers.TryGetSection(trimmed, out var s)) { section = s; continue; }
            if (!string.Equals(section, OnlineSection, StringComparison.OrdinalIgnoreCase)) continue;
            if (trimmed.StartsWith(';') || trimmed.StartsWith('#')) continue;

            var eq = trimmed.IndexOf('=');
            if (eq > 0 && trimmed[..eq].Trim().Equals(HintKey, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static (Encoding, int) DetectEncoding(byte[] b)
    {
        if (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) return (new UTF8Encoding(false), 3);
        if (b.Length >= 2 && b[0] == 0xFF && b[1] == 0xFE) return (new UnicodeEncoding(false, false), 2);
        if (b.Length >= 2 && b[0] == 0xFE && b[1] == 0xFF) return (new UnicodeEncoding(true, false), 2);
        return (new UTF8Encoding(false), 0);
    }
}
