using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Services;

namespace GitPeach.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private readonly IAppStateStore _store;
    private readonly IRepoViewModelFactory _repos;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly bool _ready;

    public HomeViewModel Home { get; }
    /// <summary>Every tab, Home first (order used by Ctrl+Tab and Ctrl+1..9).</summary>
    public ObservableCollection<TabViewModel> Tabs { get; } = new();
    /// <summary>Repository tabs only: the tab strip. Home has its own button.</summary>
    public ObservableCollection<RepoViewModel> RepoTabs { get; } = new();

    [ObservableProperty] private TabViewModel? _selectedTab;

    public MainWindowViewModel(IAppStateStore store, HomeViewModel home, IRepoViewModelFactory repos, IDialogService dialogs, IShellService shell)
    {
        _store = store;
        _repos = repos;
        _dialogs = dialogs;
        _shell = shell;
        Home = home;
        Home.OpenRequested += OpenRepo;
        Tabs.Add(Home);
        SelectedTab = Home;

        var active = store.State.ActiveTab;
        foreach (var path in store.State.OpenTabs.ToList())
        {
            if (Directory.Exists(path)) AddRepoTab(path, select: false);
        }
        SelectedTab = Tabs.OfType<RepoViewModel>().FirstOrDefault(t =>
            string.Equals(t.RepoPath, active, StringComparison.OrdinalIgnoreCase)) ?? (TabViewModel)Home;
        _ready = true;
    }

    /// <summary>
    /// <c>GitPeach.exe &lt;folder&gt;</c> (TextPeach's branch button, for instance): adds the repository if needed and
    /// opens it in a tab. A folder of repositories is imported as a group, as when it is dropped on the window.
    /// </summary>
    public async Task OpenFromArgsAsync(IEnumerable<string> args)
    {
        foreach (var raw in args)
        {
            string path;
            try { path = Path.GetFullPath(raw); } catch { continue; }
            if (Directory.Exists(path)) await Home.AddPathAsync(path, open: true);
        }
    }

    public void OpenRepo(RepoEntry entry)
    {
        _store.Touch(entry);
        AddRepoTab(entry.Path, select: true);
        SaveTabs();
    }

    private void AddRepoTab(string path, bool select)
    {
        var existing = Tabs.OfType<RepoViewModel>().FirstOrDefault(t =>
            string.Equals(t.RepoPath, path, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            if (select) SelectedTab = existing;
            return;
        }
        var tab = _repos.Create(path);
        tab.CloseCommand = new RelayCommand(() => CloseTab(tab));
        Tabs.Add(tab);
        RepoTabs.Add(tab);
        if (select) SelectedTab = tab;
    }

    public void CloseTab(TabViewModel tab)
    {
        if (!tab.Closable) return;
        int idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
        if (tab is RepoViewModel repo) RepoTabs.Remove(repo);
        (tab as IDisposable)?.Dispose();
        if (SelectedTab == tab || SelectedTab is null)
            SelectedTab = Tabs[Math.Clamp(idx - 1, 0, Tabs.Count - 1)];
        SaveTabs();
    }

    [RelayCommand]
    private void CloseCurrent()
    {
        if (SelectedTab is { Closable: true } t) CloseTab(t);
    }

    [RelayCommand]
    private void NextTab()
    {
        if (Tabs.Count < 2 || SelectedTab is null) return;
        SelectedTab = Tabs[(Tabs.IndexOf(SelectedTab) + 1) % Tabs.Count];
    }

    [RelayCommand]
    private void PreviousTab()
    {
        if (Tabs.Count < 2 || SelectedTab is null) return;
        SelectedTab = Tabs[(Tabs.IndexOf(SelectedTab) - 1 + Tabs.Count) % Tabs.Count];
    }

    /// <summary>Ctrl+1..9 (1-based, as typed).</summary>
    [RelayCommand]
    private void SelectTab(string? number)
    {
        if (int.TryParse(number, out var n) && n >= 1 && n <= Tabs.Count) SelectedTab = Tabs[n - 1];
    }

    [RelayCommand]
    private async Task QuickOpenAsync()
    {
        var picked = await _dialogs.QuickOpen(_store.State.Repos
            .OrderByDescending(r => r.Favorite).ThenByDescending(r => r.LastOpened).ToList());
        if (picked is not null) OpenRepo(picked);
    }

    [RelayCommand]
    private Task ShowAbout() => _dialogs.About(Loc.T("about.text"), _store.DataFolder, Shortcuts.Groups,
        () => _shell.OpenFolder(_store.DataFolder), _store.State.Language, code =>
        {
            _store.State.Language = code;
            _store.Save();
        }, _store.State.CheckUpdates, on =>
        {
            _store.State.CheckUpdates = on;
            _store.Save();
        }, () => CheckForUpdatesAsync(manual: true));

    // ------------------------------------------------------------ updates

    /// <summary>Text of the button next to "?": the new version, or the download progress. Null hides it.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasUpdate))]
    private string? _updateLabel;
    public bool HasUpdate => UpdateLabel is not null;
    private bool _updating;

    /// <summary>Asks the window to close (to restart into the new version).</summary>
    public event Action? CloseRequested;

    /// <summary>
    /// Automatic (at start): at most once a day, only if enabled, and silent on errors. Manual ("Check now" in
    /// About): always asks GitHub and tells the result.
    /// </summary>
    public async Task CheckForUpdatesAsync(bool manual)
    {
        var st = _store.State;
        if (!manual && (!st.CheckUpdates || st.LastUpdateCheck is { } last && DateTimeOffset.Now - last < TimeSpan.FromHours(24)))
        {
            ShowKnownUpdate();
            return;
        }
        UpdateInfo? info;
        try { info = await Updater.CheckAsync(Updater.Current); }
        catch (Exception ex)
        {
            if (manual) await _dialogs.Message(Loc.T("update.check.title"), Loc.F("update.check.failed", ex.Message));
            return;
        }
        st.LastUpdateCheck = DateTimeOffset.Now;
        st.LatestVersion = info?.Version.ToString(3);
        // Asking explicitly brings back a version that was skipped.
        if (manual && info is not null && st.SkippedVersion == st.LatestVersion) st.SkippedVersion = null;
        _store.Save();
        ShowKnownUpdate();
        if (!manual) return;
        if (info is null) await _dialogs.Message(Loc.T("update.check.title"), Loc.F("update.upToDate", Updater.Current.ToString(3)));
        else await OfferUpdateAsync(info);
    }

    private void ShowKnownUpdate()
    {
        if (_updating) return;
        var st = _store.State;
        UpdateLabel = Updater.TryParseTag(st.LatestVersion, out var v) && v > Updater.Current && st.LatestVersion != st.SkippedVersion
            ? "↑ " + st.LatestVersion
            : null;
    }

    /// <summary>The button next to "?": fresh release data (the saved check only keeps the version), then the popup.</summary>
    [RelayCommand]
    private async Task ShowUpdateAsync()
    {
        if (_updating) return;
        UpdateInfo? info;
        try { info = await Updater.CheckAsync(Updater.Current); }
        catch (Exception ex)
        {
            if (await _dialogs.Confirm(Loc.T("update.check.title"), Loc.F("update.check.failed", ex.Message), Loc.T("update.openPage")))
                _shell.OpenUrl(Updater.ReleasesPage);
            return;
        }
        if (info is null)
        {
            _store.State.LatestVersion = null;
            _store.Save();
            ShowKnownUpdate();
            return;
        }
        await OfferUpdateAsync(info);
    }

    private async Task OfferUpdateAsync(UpdateInfo info)
    {
        var choice = await _dialogs.Update(info, Updater.Current);
        if (choice == UpdateChoice.Skip)
        {
            _store.State.SkippedVersion = info.Version.ToString(3);
            _store.Save();
            ShowKnownUpdate();
            return;
        }
        if (choice != UpdateChoice.Install) return;

        _updating = true;
        UpdateLabel = Loc.F("update.downloading", 0);
        try
        {
            var next = await Updater.ApplyAsync(info, new Progress<double>(f => UpdateLabel = Loc.F("update.downloading", (int)(f * 100))));
            Updater.Launch(next);
            CloseRequested?.Invoke();
        }
        catch (Exception ex)
        {
            _updating = false;
            ShowKnownUpdate();
            if (await _dialogs.Confirm(Loc.T("update.failed.title"), Loc.F("update.failed.text", ex.Message), Loc.T("update.openPage")))
                _shell.OpenUrl(info.PageUrl);
        }
    }

    public bool IsHomeSelected => SelectedTab == Home;

    /// <summary>Selection of the tab strip; null while Home is shown (a null coming back from the strip is ignored).</summary>
    public RepoViewModel? SelectedRepoTab
    {
        get => SelectedTab as RepoViewModel;
        set { if (value is not null) SelectedTab = value; }
    }

    [RelayCommand]
    private void ShowHome() => SelectedTab = Home;

    /// <summary>Next to the app name in the title bar: the open repository and where it lives.</summary>
    public string Subtitle => SelectedTab is RepoViewModel r ? $"{r.Title}  ·  {r.RepoPath}" : "";
    /// <summary>Taskbar and Alt+Tab text.</summary>
    public string WindowTitle => SelectedTab is RepoViewModel r ? $"{r.Title} — GitPeach" : "GitPeach";

    partial void OnSelectedTabChanged(TabViewModel? value)
    {
        OnPropertyChanged(nameof(Subtitle));
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(IsHomeSelected));
        OnPropertyChanged(nameof(SelectedRepoTab));
        if (_ready) SaveTabs();
    }

    public void SaveTabs()
    {
        _store.State.OpenTabs = Tabs.OfType<RepoViewModel>().Select(t => t.RepoPath).ToList();
        _store.State.ActiveTab = (SelectedTab as RepoViewModel)?.RepoPath;
        _store.Save();
    }

    public void Shutdown()
    {
        SaveTabs();
        foreach (var t in Tabs.OfType<RepoViewModel>()) t.Dispose();
    }
}
