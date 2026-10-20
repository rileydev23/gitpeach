namespace GitPeach.Services;

/// <summary>FileSystemWatcher with a 400 ms debounce that ignores git's internal object churn.</summary>
public sealed class RepoWatcher : IRepoWatcher
{
    private readonly string _root;
    private readonly FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounce;
    private bool _disposed;

    public event Action? Changed;

    public RepoWatcher(string root)
    {
        _root = root;
        try
        {
            _watcher = new FileSystemWatcher(root)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                InternalBufferSize = 64 * 1024,
            };
            FileSystemEventHandler h = (_, e) => OnFsEvent(e.FullPath);
            _watcher.Changed += h;
            _watcher.Created += h;
            _watcher.Deleted += h;
            _watcher.Renamed += (_, e) => OnFsEvent(e.FullPath);
            _watcher.Error += (_, _) => Schedule();
            _watcher.EnableRaisingEvents = true;
        }
        catch { /* no watching; manual refresh still works */ }
    }

    /// <summary>True for paths whose changes never affect what the UI shows.</summary>
    public static bool IsNoise(string root, string fullPath)
    {
        var rel = fullPath.Length > root.Length ? fullPath[root.Length..] : "";
        return rel.Contains(@"\.git\objects", StringComparison.OrdinalIgnoreCase) ||
               rel.Contains(@"\.git\lfs", StringComparison.OrdinalIgnoreCase) ||
               rel.EndsWith(".lock", StringComparison.OrdinalIgnoreCase);
    }

    private void OnFsEvent(string fullPath)
    {
        if (!IsNoise(_root, fullPath)) Schedule();
    }

    private void Schedule()
    {
        if (_disposed) return;
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        _ = Task.Delay(400, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled && !_disposed) Changed?.Invoke();
        }, TaskScheduler.Default);
    }

    public void Dispose()
    {
        _disposed = true;
        _debounce?.Cancel();
        _watcher?.Dispose();
    }
}

public sealed class RepoWatcherFactory : IRepoWatcherFactory
{
    public IRepoWatcher Watch(string path) => new RepoWatcher(path);
}
