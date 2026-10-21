namespace GitPeach.Services;

// View-model-facing abstractions. View models never touch Avalonia windows, Process or the
// clipboard directly, which keeps them testable and the UI swappable (dependency inversion).

public interface IDialogService
{
    Task<bool> Confirm(string title, string message, string ok = "Aceptar", string cancel = "Cancelar");
    Task Message(string title, string message);
    /// <summary>F1 / "?": app name, version and description, then the keyboard shortcuts.</summary>
    Task About(string description, string dataFolder, IReadOnlyList<(string Title, (string Gesture, string Description)[] Items)> shortcutGroups, Action openDataFolder);
    Task<string?> Prompt(string title, string label, string initial = "");
    Task<string[]?> Form(string title, (string Label, string Initial)[] fields, string ok = "Aceptar");
    /// <summary>Lets the user pick one of <paramref name="options"/>; null when cancelled.</summary>
    Task<string?> Choose(string title, string label, IReadOnlyList<string> options, string ok = "Aceptar");
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
    /// <summary>Folder that holds state.json (shown in "Acerca de").</summary>
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
