using SiegeServerChanger.Core;

namespace SiegeServerChanger.Tests;

public class DataCenterCatalogTests
{
    private static readonly string[] Lines =
    [
        "[ONLINE]",
        ";DataCenterHint => ",
        ";    default (ping based)",
        ";    gamelift/ap-northeast-2",
        ";    gamelift/xx-new-9",
        ";    playfab/uaenorth",
        "",
        ";",
        ";if UseProxyAutoDiscovery is set to true",
        "DataCenterHint=default",
    ];

    [Fact]
    public void Parses_values_from_comment_block()
    {
        var ids = DataCenterCatalog.ParseFromLines(Lines).Select(d => d.Id);
        Assert.Equal(["default", "gamelift/ap-northeast-2", "gamelift/xx-new-9", "playfab/uaenorth"], ids);
    }

    [Fact]
    public void Known_ids_get_korean_names_and_unknown_keep_code()
    {
        var list = DataCenterCatalog.ParseFromLines(Lines);
        Assert.Equal("서울", list.Single(d => d.Id == "gamelift/ap-northeast-2").DisplayName);
        Assert.Equal("gamelift/xx-new-9", list.Single(d => d.Id == "gamelift/xx-new-9").DisplayName);
    }

    [Fact]
    public void Falls_back_when_no_comment_block_and_default_is_first()
    {
        var list = DataCenterCatalog.Load(["[ONLINE]", "DataCenterHint=default"]);
        Assert.True(list.Count > 10);
        Assert.Equal("default", list[0].Id);
    }

    [Fact]
    public void Ignores_comment_block_outside_online_section()
    {
        Assert.Empty(DataCenterCatalog.ParseFromLines(["[OTHER]", ";DataCenterHint =>", ";    gamelift/us-east-1"]));
    }

    [Fact]
    public void Region_strips_provider()
    {
        Assert.Equal("ap-northeast-2", DataCenterCatalog.Create("gamelift/ap-northeast-2").Region);
    }
}

public class ProfileLocatorTests
{
    [Fact]
    public void Finds_only_folders_with_settings_file()
    {
        var root = Directory.CreateTempSubdirectory("siege-root-").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "Benchmark"));
            var a = Directory.CreateDirectory(Path.Combine(root, "aaaa-1111")).FullName;
            File.WriteAllText(Path.Combine(a, "GameSettings.ini"), "[ONLINE]");

            var profiles = ProfileLocator.Find(root);
            Assert.Single(profiles);
            Assert.Equal("aaaa-1111", profiles[0].Id);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void Missing_root_returns_empty()
    {
        Assert.Empty(ProfileLocator.Find(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())));
    }
}
