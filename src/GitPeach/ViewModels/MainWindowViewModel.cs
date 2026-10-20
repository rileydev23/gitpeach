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
    private readonly bool _ready;

    public HomeViewModel Home { get; }
    public ObservableCollection<TabViewModel> Tabs { get; } = new();

    [ObservableProperty] private TabViewModel? _selectedTab;

    public MainWindowViewModel(IAppStateStore store, HomeViewModel home, IRepoViewModelFactory repos, IDialogService dialogs)
    {
        _store = store;
        _repos = repos;
        _dialogs = dialogs;
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
        if (select) SelectedTab = tab;
    }

    public void CloseTab(TabViewModel tab)
    {
        if (!tab.Closable) return;
        int idx = Tabs.IndexOf(tab);
        Tabs.Remove(tab);
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
    private Task ShowShortcuts() => _dialogs.Message("Atajos de teclado", Shortcuts.Describe());

    partial void OnSelectedTabChanged(TabViewModel? value)
    {
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
