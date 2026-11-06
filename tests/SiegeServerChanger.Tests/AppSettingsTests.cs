using SiegeServerChanger.Core;

namespace SiegeServerChanger.Tests;

public class AppSettingsTests
{
    private static DataCenter D(string id) => DataCenterCatalog.Create(id);

    [Fact]
    public void Toggle_favorite_adds_then_removes_case_insensitive()
    {
        var s = new AppSettings();
        Assert.True(s.ToggleFavorite("gamelift/ap-northeast-2"));
        Assert.True(s.IsFavorite("GAMELIFT/AP-NORTHEAST-2"));
        Assert.False(s.ToggleFavorite("GameLift/ap-northeast-2"));
        Assert.Empty(s.Favorites);
    }

    [Fact]
    public void Recent_is_mru_without_duplicates_and_capped()
    {
        var s = new AppSettings();
        foreach (var id in new[] { "a", "b", "c", "d", "e", "f", "c" }) s.AddRecent(id);
        Assert.Equal(["c", "f", "e", "d", "b"], s.Recent);
    }

    [Fact]
    public void Order_puts_default_then_favorites_then_rest()
    {
        var s = new AppSettings { Favorites = ["gamelift/us-east-1", "gamelift/ap-northeast-1"] };
        var ordered = s.Order([D("gamelift/ap-northeast-2"), D("gamelift/ap-northeast-1"), D("default"), D("gamelift/eu-west-1"), D("gamelift/us-east-1")])
            .Select(d => d.Id);
        Assert.Equal(["default", "gamelift/us-east-1", "gamelift/ap-northeast-1", "gamelift/ap-northeast-2", "gamelift/eu-west-1"], ordered);
    }

    [Fact]
    public void Store_round_trips_and_tolerates_corrupt_or_missing_file()
    {
        var dir = Directory.CreateTempSubdirectory("siege-settings-").FullName;
        try
        {
            var path = Path.Combine(dir, "sub", "settings.json");
            Assert.Empty(AppSettingsStore.Load(path).Favorites);

            var s = new AppSettings { Favorites = ["x"], Recent = ["y"], Launcher = LauncherKind.Ubisoft };
            AppSettingsStore.Save(s, path);
            var loaded = AppSettingsStore.Load(path);
            Assert.Equal(["x"], loaded.Favorites);
            Assert.Equal(["y"], loaded.Recent);
            Assert.Equal(LauncherKind.Ubisoft, loaded.Launcher);

            File.WriteAllText(path, "{ not json");
            Assert.Empty(AppSettingsStore.Load(path).Favorites);

            File.WriteAllText(path, """{"Favorites":null,"Recent":null}""");
            var nulls = AppSettingsStore.Load(path);
            Assert.NotNull(nulls.Favorites);
            Assert.NotNull(nulls.Recent);
        }
        finally { Directory.Delete(dir, true); }
    }

    [Theory]
    [InlineData(LauncherKind.Steam, "steam://rungameid/359550")]
    [InlineData(LauncherKind.Ubisoft, "uplay://launch/635/0")]
    public void Launcher_uris(LauncherKind kind, string uri) => Assert.Equal(uri, GameLauncher.GetUri(kind));
}
