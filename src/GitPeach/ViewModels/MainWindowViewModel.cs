using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Services;
using GitPeach.Views;

namespace GitPeach.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public RepoStore Store { get; }
    public HomeViewModel Home { get; }
    public ObservableCollection<TabViewModel> Tabs { get; } = new();

    [ObservableProperty] private TabViewModel? _selectedTab;
    private readonly bool _ready;

    public MainWindowViewModel(RepoStore store)
    {
        Store = store;
        Home = new HomeViewModel(store, OpenRepo);
        Tabs.Add(Home);
        SelectedTab = Home;

        var saved = store.State.OpenTabs.ToList();
        var active = store.State.ActiveTab;
        foreach (var path in saved)
        {
            if (Directory.Exists(path)) AddRepoTab(path, select: false);
        }
        SelectedTab = Tabs.OfType<RepoViewModel>().FirstOrDefault(t =>
            string.Equals(t.RepoPath, active, StringComparison.OrdinalIgnoreCase)) ?? (TabViewModel)Home;
        _ready = true;
    }

    public void OpenRepo(RepoEntry entry)
    {
        Store.Touch(entry);
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
        var tab = new RepoViewModel(path);
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

    [RelayCommand]
    private async Task QuickOpenAsync()
    {
        var picked = await Dialogs.QuickOpen(Store.State.Repos
            .OrderByDescending(r => r.Favorite).ThenByDescending(r => r.LastOpened).ToList());
        if (picked is not null) OpenRepo(picked);
    }

    partial void OnSelectedTabChanged(TabViewModel? value)
    {
        if (_ready) SaveTabs();
    }

    public void SaveTabs()
    {
        Store.State.OpenTabs = Tabs.OfType<RepoViewModel>().Select(t => t.RepoPath).ToList();
        Store.State.ActiveTab = (SelectedTab as RepoViewModel)?.RepoPath;
        Store.Save();
    }

    public void Shutdown()
    {
        SaveTabs();
        foreach (var t in Tabs.OfType<RepoViewModel>()) t.Dispose();
    }
}
