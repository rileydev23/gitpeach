namespace GitPeach.Services;

// View-model-facing abstractions. View models never touch Avalonia windows, Process or the
// clipboard directly, which keeps them testable and the UI swappable (dependency inversion).

public enum UpdateChoice { Later, Install, Skip }

public interface IDialogService
{
    /// <summary>Null buttons read "OK" / "Cancel" in the interface language.</summary>
    Task<bool> Confirm(string title, string message, string? ok = null, string? cancel = null);
    Task Message(string title, string message);
    /// <summary>F1 / "?": app name, version and description, then the keyboard shortcuts.</summary>
    /// <summary>Also the language switch: <paramref name="savedLanguage"/> is the saved choice (null = system).</summary>
    /// <summary>And the updates row: <paramref name="checkUpdates"/> is the saved switch, <paramref name="checkNow"/> runs after closing.</summary>
    Task About(string description, string dataFolder, IReadOnlyList<(string Title, (string Gesture, string Description)[] Items)> shortcutGroups,
        Action openDataFolder, string? savedLanguage, Action<string> chooseLanguage,
        bool checkUpdates, Action<bool> setCheckUpdates, Func<Task> checkNow);
    /// <summary>The button next to "?": release notes and Update / Not now / Skip. Closing it is "Not now".</summary>
    Task<UpdateChoice> Update(UpdateInfo update, Version current);
    Task<string?> Prompt(string title, string label, string initial = "");
    Task<string[]?> Form(string title, (string Label, string Initial)[] fields, string? ok = null);
    /// <summary>Lets the user pick one of <paramref name="options"/>; null when cancelled.</summary>
    Task<string?> Choose(string title, string label, IReadOnlyList<string> options, string? ok = null);
    Task<string?> PickFolder(string title);
    Task<RepoEntry?> QuickOpen(IReadOnlyList<RepoEntry> repos);
}

public interface IClipboardService
{
    Task SetTextAsync(string text);
}

/// <summary>Launches external programs (Explorer, terminal, TextPeach, default app, browser).</summary>
public interface IShellService
{
    void OpenFolder(string path);
    void RevealInExplorer(string path);
    void OpenFile(string path);
    void OpenTerminal(string directory);
    /// <summary>Opens a folder (and optionally one of its files) in TextPeach. False when TextPeach is not installed.</summary>
    bool OpenInTextPeach(string folder, string? file = null);
    void OpenUrl(string url);
}

/// <summary>Opens a view model in its own window (the view is resolved by convention).</summary>
public interface IWindowService
{
    void Show(object viewModel);
}

/// <summary>Persisted application state (repository list, open tabs, window placement).</summary>
public interface IAppStateStore
{
    AppState State { get; }
    /// <summary>Folder that holds state.json (shown in About).</summary>
    string DataFolder { get; }
    void Save();
    RepoEntry? Find(string path);
    RepoEntry Add(string path);
    void Remove(RepoEntry e);
    void Touch(RepoEntry e);
}

/// <summary>Watches a working tree and reports (debounced) that something changed.</summary>
public interface IRepoWatcher : IDisposable
{
    event Action? Changed;
}

public interface IRepoWatcherFactory
{
    IRepoWatcher Watch(string path);
}
