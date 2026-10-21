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
    /// <c>GitPeach.exe &lt;carpeta&gt;</c> (TextPeach's branch button, for instance): adds the repository if needed and
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
    private Task ShowAbout() => _dialogs.About(Shortcuts.AboutText, _store.DataFolder, Shortcuts.Groups, () => _shell.OpenFolder(_store.DataFolder));

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
