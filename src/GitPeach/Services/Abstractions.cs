namespace GitPeach.Services;

// View-model-facing abstractions. View models never touch Avalonia windows, Process or the
// clipboard directly, which keeps them testable and the UI swappable (dependency inversion).

public interface IDialogService
{
    Task<bool> Confirm(string title, string message, string ok = "Aceptar");
    Task Message(string title, string message);
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

/// <summary>Launches external programs (Explorer, terminal, editor, default app).</summary>
public interface IShellService
{
    void OpenFolder(string path);
    void RevealInExplorer(string path);
    void OpenFile(string path);
    void OpenTerminal(string directory);
    void OpenInEditor(string path);
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
