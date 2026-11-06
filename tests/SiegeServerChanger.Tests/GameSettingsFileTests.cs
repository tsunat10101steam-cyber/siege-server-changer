using System.Text;
using SiegeServerChanger.Core;

namespace SiegeServerChanger.Tests;

public class GameSettingsFileTests
{
    // Trimmed copy of a real GameSettings.ini (2026-11).
    private const string Sample =
        "[DISPLAY]\r\n" +
        "DataCenterHint=not-this-one\r\n" +
        "\r\n" +
        "[ONLINE]\r\n" +
        ";DataCenterHint => \r\n" +
        ";    default (ping based)\r\n" +
        ";    gamelift/ap-northeast-1\r\n" +
        ";    gamelift/ap-northeast-2\r\n" +
        ";    gamelift/us-east-1\r\n" +
        ";    playfab/uaenorth\r\n" +
        "\r\n" +
        ";\r\n" +
        ";if UseProxyAutoDiscovery is set to true, http_proxy ... are going to be looked up\r\n" +
        "DataCenterHint=gamelift/ap-northeast-2\r\n" +
        "UseProxyAutoDiscovery=0\r\n";

    private static GameSettingsFile Parse(string text, Encoding? enc = null, bool bom = false)
    {
        enc ??= new UTF8Encoding(false);
        var body = enc.GetBytes(text);
        var bytes = bom ? enc.GetPreamble().Concat(body).ToArray() : body;
        return GameSettingsFile.Parse("x.ini", bytes);
    }

    [Fact]
    public void Reads_hint_from_online_section_only()
    {
        Assert.Equal("gamelift/ap-northeast-2", Parse(Sample).GetDataCenterHint());
    }

    [Fact]
    public void Unchanged_file_round_trips_byte_for_byte()
    {
        var bytes = Encoding.UTF8.GetBytes(Sample);
        Assert.Equal(bytes, GameSettingsFile.Parse("x.ini", bytes).ToBytes());
    }

    [Fact]
    public void Set_changes_exactly_one_line()
    {
        var f = Parse(Sample);
        f.SetDataCenterHint("gamelift/ap-northeast-1");

        var before = Sample.Split("\r\n");
        var after = Encoding.UTF8.GetString(f.ToBytes()).Split("\r\n");
        Assert.Equal(before.Length, after.Length);

        var diffs = before.Zip(after).Where(p => p.First != p.Second).ToList();
        Assert.Single(diffs);
        Assert.Equal("DataCenterHint=gamelift/ap-northeast-1", diffs[0].Second);
        Assert.Equal("gamelift/ap-northeast-1", f.GetDataCenterHint());
    }

    [Fact]
    public void Preserves_lf_line_endings_and_missing_trailing_newline()
    {
        var text = "[ONLINE]\nDataCenterHint=default";
        var f = Parse(text);
        f.SetDataCenterHint("gamelift/us-east-1");
        Assert.Equal("[ONLINE]\nDataCenterHint=gamelift/us-east-1", Encoding.UTF8.GetString(f.ToBytes()));
    }

    [Fact]
    public void Preserves_utf8_bom()
    {
        var f = Parse(Sample, Encoding.UTF8, bom: true);
        f.SetDataCenterHint("default");
        var bytes = f.ToBytes();
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact]
    public void Preserves_utf16_encoding()
    {
        var enc = new UnicodeEncoding(false, true);
        var f = Parse(Sample, enc, bom: true);
        Assert.Equal("gamelift/ap-northeast-2", f.GetDataCenterHint());
        f.SetDataCenterHint("default");
        var bytes = f.ToBytes();
        Assert.Equal(new byte[] { 0xFF, 0xFE }, bytes[..2]);
        Assert.Contains("DataCenterHint=default", enc.GetString(bytes, 2, bytes.Length - 2));
    }

    [Fact]
    public void Set_throws_when_key_missing_instead_of_guessing()
    {
        var f = Parse("[ONLINE]\r\nUseProxyAutoDiscovery=0\r\n");
        Assert.Null(f.GetDataCenterHint());
        Assert.Throws<InvalidOperationException>(() => f.SetDataCenterHint("default"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a\r\nb")]
    public void Set_rejects_bad_values(string value)
    {
        Assert.Throws<ArgumentException>(() => Parse(Sample).SetDataCenterHint(value));
    }

    [Fact]
    public void Save_creates_backup_once_and_restore_works()
    {
        var dir = Directory.CreateTempSubdirectory("siege-test-").FullName;
        try
        {
            var path = Path.Combine(dir, "GameSettings.ini");
            File.WriteAllText(path, Sample);

            var f = GameSettingsFile.Load(path);
            f.SetDataCenterHint("gamelift/us-east-1");
            f.Save();
            Assert.Equal(Sample, File.ReadAllText(f.BackupPath));

            f = GameSettingsFile.Load(path);
            f.SetDataCenterHint("default");
            f.Save();
            Assert.Equal(Sample, File.ReadAllText(f.BackupPath)); // backup keeps the original
            Assert.Equal("default", GameSettingsFile.Load(path).GetDataCenterHint());

            GameSettingsFile.RestoreBackup(path);
            Assert.Equal(Sample, File.ReadAllText(path));
        }
        finally { Directory.Delete(dir, true); }
    }

    [Fact]
    public void Save_refuses_read_only_file()
    {
        var dir = Directory.CreateTempSubdirectory("siege-test-").FullName;
        var path = Path.Combine(dir, "GameSettings.ini");
        try
        {
            File.WriteAllText(path, Sample);
            File.SetAttributes(path, FileAttributes.ReadOnly);
            var f = GameSettingsFile.Load(path);
            f.SetDataCenterHint("default");
            Assert.Throws<IOException>(f.Save);
        }
        finally
        {
            File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }
    }
}
