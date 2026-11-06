using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using SiegeServerChanger.Core;

namespace SiegeServerChanger.App;

public enum PingState { None, Measuring, Done, Unavailable }

public sealed class ServerItem(DataCenter server, bool isCurrent, bool isFavorite) : INotifyPropertyChanged
{
    private bool _isFavorite = isFavorite;

    public bool CanFavorite => Server.Id != DataCenter.DefaultId;
    public string StarText => !CanFavorite ? "" : _isFavorite ? "★" : "☆";
    public string StarTip => _isFavorite ? "즐겨찾기 해제" : "즐겨찾기에 추가 (목록 위로 올라감)";
    public bool IsFavorite
    {
        get => _isFavorite;
        set
        {
            _isFavorite = value;
            PropertyChanged?.Invoke(this, new(nameof(StarText)));
            PropertyChanged?.Invoke(this, new(nameof(StarTip)));
        }
    }

    private static readonly Brush Good = Frozen("#4CC38A"), Mid = Frozen("#F5B83D"), Bad = Frozen("#F06A6A"), Muted = Frozen("#9AA0AA");

    private PingState _state;
    private double? _ms;

    public DataCenter Server { get; } = server;
    public bool IsCurrent { get; } = isCurrent;
    public string DisplayName => Server.DisplayName;
    public string Region => Server.Id == DataCenter.DefaultId ? "" : Server.Region;
    public string Marker => IsCurrent ? "현재" : "";
    public double? PingMs => _state == PingState.Done ? _ms : null;

    public string PingText => _state switch
    {
        PingState.Measuring => "…",
        PingState.Done => $"{_ms:0}ms",
        PingState.Unavailable => Server.Id == DataCenter.DefaultId ? "" : "측정 불가",
        _ => "",
    };

    public Brush PingBrush => _state != PingState.Done ? Muted : _ms < 60 ? Good : _ms < 120 ? Mid : Bad;

    public void SetPing(PingState state, double? ms = null)
    {
        _state = state;
        _ms = ms;
        PropertyChanged?.Invoke(this, new(nameof(PingText)));
        PropertyChanged?.Invoke(this, new(nameof(PingBrush)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Used as the accessible name of the list item.
    public override string ToString() => DisplayName;

    private static Brush Frozen(string hex)
    {
        var b = (Brush)new BrushConverter().ConvertFromString(hex)!;
        b.Freeze();
        return b;
    }
}

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _gameWatch = new() { Interval = TimeSpan.FromSeconds(2) };
    private IReadOnlyList<SiegeProfile> _profiles = [];
    // Ping results survive list rebuilds (apply / profile switch / refresh).
    private readonly Dictionary<string, (PingState State, double? Ms)> _pings = new(StringComparer.OrdinalIgnoreCase);
    private bool _measuring;
    private readonly AppSettings _settings = AppSettingsStore.Load();
    private readonly IReadOnlyList<LauncherKind> _launchers = GameLauncher.DetectInstalled();

    private IEnumerable<ServerItem> Items => ServerList.ItemsSource as IEnumerable<ServerItem> ?? [];

    public MainWindow()
    {
        InitializeComponent();
        _gameWatch.Tick += (_, _) => UpdateGameWarning();
        _gameWatch.Start();
        SetupLauncher();
        Loaded += (_, _) => LoadProfiles();
    }

    private void SetupLauncher()
    {
        LauncherBox.ItemsSource = _launchers.Select(GameLauncher.GetName).ToList();
        LauncherBox.Visibility = _launchers.Count > 1 ? Visibility.Visible : Visibility.Collapsed;
        LaunchButton.IsEnabled = _launchers.Count > 0;
        LaunchButton.ToolTip = _launchers.Count > 0 ? null : "Steam/Ubisoft Connect에서 설치된 레식을 찾지 못했습니다.";
        if (_launchers.Count == 0) return;
        var idx = _settings.Launcher is { } pref ? _launchers.ToList().IndexOf(pref) : -1;
        LauncherBox.SelectedIndex = idx >= 0 ? idx : 0;
    }

    private void SaveSettings()
    {
        try { AppSettingsStore.Save(_settings); }
        catch (Exception ex) { SetStatus($"프로그램 설정 저장 실패: {ex.Message}"); }
    }

    private SiegeProfile? SelectedProfile => ProfileBox.SelectedItem as SiegeProfile;

    private void LoadProfiles(string? keepId = null)
    {
        keepId ??= SelectedProfile?.Id;
        _profiles = ProfileLocator.Find();
        ProfileBox.ItemsSource = _profiles;
        AllProfilesBox.Visibility = _profiles.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

        if (_profiles.Count == 0)
        {
            CurrentText.Text = "프로필 없음";
            ServerList.ItemsSource = null;
            ApplyButton.IsEnabled = RestoreButton.IsEnabled = false;
            SetStatus($"설정 파일을 찾지 못했습니다. 게임을 한 번 실행한 뒤 다시 시도해 주세요.\n찾은 위치: {ProfileLocator.GetSiegeRoot()}");
            return;
        }

        ProfileBox.SelectedItem = _profiles.FirstOrDefault(p => p.Id == keepId) ?? _profiles[0];
        UpdateGameWarning();
    }

    /// <param name="keepSelectionId">Re-select this server instead of the current one (e.g. after starring).</param>
    private void LoadSelectedProfile(string? keepSelectionId = null)
    {
        if (SelectedProfile is not { } profile) return;
        try
        {
            var file = GameSettingsFile.Load(profile.SettingsPath);
            var current = file.GetDataCenterHint();
            var servers = DataCenterCatalog.Load(file.Lines).ToList();

            // Keep an unknown current value visible instead of silently dropping it.
            if (current is not null && !servers.Any(s => Same(s.Id, current)))
                servers.Add(DataCenterCatalog.Create(current));

            var items = _settings.Order(servers)
                .Select(s => new ServerItem(s, current is not null && Same(s.Id, current), _settings.IsFavorite(s.Id)))
                .ToList();
            foreach (var it in items)
                if (_pings.TryGetValue(it.Server.Id, out var p)) it.SetPing(p.State, p.Ms);
            ServerList.ItemsSource = items;
            UpdateFastestButton();
            UpdateRecent(items);
            ServerList.SelectedItem = (keepSelectionId is null ? null : items.FirstOrDefault(i => Same(i.Server.Id, keepSelectionId)))
                ?? items.FirstOrDefault(i => i.IsCurrent);
            ServerList.ScrollIntoView(ServerList.SelectedItem ?? items.FirstOrDefault());

            CurrentText.Text = current is null
                ? "알 수 없음 (DataCenterHint 항목 없음)"
                : $"{DataCenterCatalog.GetDisplayName(current)}  ·  {current}";
            ApplyButton.IsEnabled = current is not null;
            RestoreButton.IsEnabled = file.HasBackup;
            SetStatus(current is null ? "이 파일에서 서버 설정 줄을 찾지 못해 수정할 수 없습니다." : "");
        }
        catch (Exception ex)
        {
            CurrentText.Text = "읽기 실패";
            ServerList.ItemsSource = null;
            ApplyButton.IsEnabled = false;
            SetStatus($"설정 파일을 읽지 못했습니다: {ex.Message}");
        }
    }

    private void Apply()
    {
        if (ServerList.SelectedItem is not ServerItem item) { SetStatus("서버를 먼저 선택해 주세요."); return; }

        if (GameProcessDetector.IsGameRunning())
        {
            var r = MessageBox.Show(this,
                "게임이 실행 중입니다. 게임은 종료할 때 설정을 다시 저장하므로 지금 바꾼 값이 덮어써질 수 있습니다.\n\n그래도 적용할까요?",
                "게임 실행 중", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (r != MessageBoxResult.Yes) return;
        }

        IReadOnlyList<SiegeProfile> targets = AllProfilesBox.IsChecked == true && AllProfilesBox.Visibility == Visibility.Visible
            ? _profiles
            : SelectedProfile is { } p ? [p] : [];

        var failures = new List<string>();
        foreach (var profile in targets)
        {
            try
            {
                var file = GameSettingsFile.Load(profile.SettingsPath);
                file.SetDataCenterHint(item.Server.Id);
                file.Save();
            }
            catch (Exception ex)
            {
                failures.Add($"{profile.Id}: {ex.Message}");
            }
        }

        var ok = targets.Count - failures.Count;
        if (ok > 0) { _settings.AddRecent(item.Server.Id); SaveSettings(); }
        LoadSelectedProfile();
        SetStatus(failures.Count == 0
            ? $"✔ {item.DisplayName}(으)로 변경했습니다 ({ok}개 프로필). 게임을 다시 시작하면 적용됩니다."
            : $"{ok}개 성공, {failures.Count}개 실패\n" + string.Join("\n", failures));
    }

    private void Restore_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedProfile is not { } profile) return;
        var r = MessageBox.Show(this, "처음 수정하기 전의 설정 파일로 되돌릴까요?\n(이 프로필에만 적용됩니다)",
            "백업 복원", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        try
        {
            GameSettingsFile.RestoreBackup(profile.SettingsPath);
            LoadSelectedProfile();
            SetStatus("✔ 백업에서 복원했습니다.");
        }
        catch (Exception ex) { SetStatus($"복원 실패: {ex.Message}"); }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dir = SelectedProfile is { } p ? Path.GetDirectoryName(p.SettingsPath)! : ProfileLocator.GetSiegeRoot();
        if (!Directory.Exists(dir)) { SetStatus($"폴더가 없습니다: {dir}"); return; }
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
    }

    private async Task MeasurePingsAsync()
    {
        if (_measuring) return;
        var items = Items.ToList();
        if (items.Count == 0) return;

        _measuring = true;
        PingButton.IsEnabled = FastestButton.IsEnabled = false;
        PingButton.Content = "측정 중…";
        SetStatus("핑을 측정하는 중입니다…");
        foreach (var it in items) it.SetPing(PingState.Measuring);

        try
        {
            await LatencyProbe.MeasureAllAsync(items.Select(i => i.Server), (dc, ms) =>
                Dispatcher.Invoke(() =>
                {
                    var state = ms is null ? PingState.Unavailable : PingState.Done;
                    _pings[dc.Id] = (state, ms);
                    // The list may have been rebuilt meanwhile; update whatever is shown now.
                    foreach (var it in Items.Where(i => Same(i.Server.Id, dc.Id))) it.SetPing(state, ms);
                }));

            var best = Fastest();
            SetStatus(best is null
                ? "측정할 수 있는 서버가 없습니다. 인터넷 연결을 확인해 주세요."
                : $"측정 완료. 가장 빠른 서버: {best.DisplayName} ({best.PingMs:0}ms)");
        }
        catch (Exception ex)
        {
            SetStatus($"핑 측정 실패: {ex.Message}");
        }
        finally
        {
            _measuring = false;
            PingButton.IsEnabled = true;
            PingButton.Content = "다시 측정";
            UpdateFastestButton();
        }
    }

    private ServerItem? Fastest() =>
        Items.Where(i => i.PingMs is not null).MinBy(i => i.PingMs);

    private void UpdateFastestButton() => FastestButton.IsEnabled = !_measuring && Fastest() is not null;

    private void SelectFastest()
    {
        if (Fastest() is not { } best) return;
        ServerList.SelectedItem = best;
        ServerList.ScrollIntoView(best);
        ServerList.Focus();
        SetStatus(best.IsCurrent
            ? $"{best.DisplayName}({best.PingMs:0}ms)이(가) 가장 빠르고, 이미 현재 서버입니다."
            : $"{best.DisplayName}({best.PingMs:0}ms)을(를) 선택했습니다. [적용]을 눌러야 바뀝니다.");
    }

    private void UpdateRecent(IReadOnlyList<ServerItem> items)
    {
        // Only servers that still exist in this profile's list; skip the one already in use.
        var recent = _settings.Recent
            .Select(id => items.FirstOrDefault(i => Same(i.Server.Id, id)))
            .Where(i => i is not null && !i.IsCurrent)
            .ToList();
        RecentList.ItemsSource = recent;
        RecentRow.Visibility = recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Star_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ServerItem item || !item.CanFavorite) return;
        var selectedId = (ServerList.SelectedItem as ServerItem)?.Server.Id;
        item.IsFavorite = _settings.ToggleFavorite(item.Server.Id);
        SaveSettings();
        LoadSelectedProfile(selectedId);
        e.Handled = true;
    }

    private void Recent_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not ServerItem item) return;
        var match = Items.FirstOrDefault(i => Same(i.Server.Id, item.Server.Id));
        if (match is null) return;
        ServerList.SelectedItem = match;
        ServerList.ScrollIntoView(match);
        SetStatus($"{match.DisplayName}을(를) 선택했습니다. [적용]을 눌러야 바뀝니다.");
    }

    private LauncherKind? SelectedLauncher =>
        LauncherBox.SelectedIndex >= 0 && LauncherBox.SelectedIndex < _launchers.Count ? _launchers[LauncherBox.SelectedIndex] : null;

    private void LauncherBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SelectedLauncher is { } k && _settings.Launcher != k) { _settings.Launcher = k; SaveSettings(); }
    }

    private void Launch_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedLauncher is not { } kind) return;
        if (GameProcessDetector.IsGameRunning()) { SetStatus("게임이 이미 실행 중입니다."); return; }
        try
        {
            GameLauncher.Launch(kind);
            SetStatus($"{GameLauncher.GetName(kind)}(으)로 게임을 실행합니다…");
        }
        catch (Exception ex) { SetStatus($"게임 실행 실패: {ex.Message}"); }
    }

    private void UpdateGameWarning() =>
        WarnBar.Visibility = GameProcessDetector.IsGameRunning() ? Visibility.Visible : Visibility.Collapsed;

    private void SetStatus(string text) => StatusText.Text = text;

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void ProfileBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => LoadSelectedProfile();
    private void Apply_Click(object sender, RoutedEventArgs e) => Apply();
    private async void Ping_Click(object sender, RoutedEventArgs e) => await MeasurePingsAsync();
    private void Fastest_Click(object sender, RoutedEventArgs e) => SelectFastest();
    private void Refresh_Click(object sender, RoutedEventArgs e) { LoadProfiles(); SetStatus("새로고침했습니다."); }
    private void ServerList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        // Only a double-click on a row applies; not on the star button or the scrollbar.
        for (var d = e.OriginalSource as DependencyObject; d is not null && d != ServerList; d = VisualTreeHelper.GetParent(d))
        {
            if (d is System.Windows.Controls.Primitives.ButtonBase) return;
            if (d is System.Windows.Controls.ListBoxItem) { Apply(); return; }
        }
    }
}
