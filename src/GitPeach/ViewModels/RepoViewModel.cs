using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Git;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>
/// A repository tab. Composes the child view models (sidebar, history, working tree, details, log),
/// owns the refresh cycle and implements <see cref="IRepoSession"/> for them.
/// </summary>
public sealed partial class RepoViewModel : TabViewModel, IRepoSession, IDisposable
{
    private readonly IGitRepository _git;
    private readonly IRepoWatcher _watcher;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private bool _refreshQueued;
    private bool _disposed;
    private string _refsSignature = "";

    public string RepoPath { get; }
    public RepoSnapshot Snapshot { get; private set; } = new();

    public RepoActions Actions { get; }
    public SidebarViewModel Sidebar { get; }
    public HistoryViewModel History { get; }
    public WorkingTreeViewModel WorkingTree { get; }
    public DetailsViewModel Details { get; }
    public CommandLogViewModel Log { get; } = new();

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyText = "";
    [ObservableProperty] private string _notice = "";
    [ObservableProperty] private string _branchText = "";
    [ObservableProperty] private string _trackText = "";
    [ObservableProperty] private string _operationText = "";
    [ObservableProperty] private bool _hasOperation;

    public RepoViewModel(string path, IGitRepository git, IRepoWatcher watcher, IDialogService dialogs,
        IShellService shell, IClipboardService clipboard, IWindowService windows)
    {
        RepoPath = path;
        Title = Path.GetFileName(path.TrimEnd('\\', '/'));
        _git = git;
        _watcher = watcher;
        _dialogs = dialogs;
        _shell = shell;

        Actions = new RepoActions(this, git, dialogs);
        var files = new FileActions(this, git, dialogs, shell, clipboard, windows);
        Sidebar = new SidebarViewModel(Actions, clipboard);
        History = new HistoryViewModel(git, Actions, clipboard) { Notify = Notify };
        WorkingTree = new WorkingTreeViewModel(this, git, git, files);
        Details = new DetailsViewModel(Title, git, new HunkActions(this, git, dialogs), windows);

        _git.CommandLogged += text => Dispatcher.UIThread.Post(() => Log.Append(text));
        _watcher.Changed += () => Dispatcher.UIThread.Post(() => _ = RefreshAllAsync(reloadCommits: false));
        History.SelectionChanged += OnCommitRowSelected;
        WorkingTree.SelectionChanged += () =>
        {
            if (Details.IsWipMode) _ = Details.ShowWorkingAsync(WorkingTree.SelectedChange);
        };
        Sidebar.SelectHashRequested += History.SelectHash;

        _ = RefreshAllAsync(reloadCommits: true);
    }

    private void OnCommitRowSelected(CommitRowVM row)
    {
        if (row.IsWip) _ = Details.ShowWorkingAsync(WorkingTree.SelectedChange);
        else if (row.Commit is { } c) _ = Details.ShowCommitAsync(c);
    }

    // ------------------------------------------------------------ refresh

    [RelayCommand]
    private Task Refresh() => RefreshAllAsync(reloadCommits: true);

    public Task RefreshAsync(bool reloadCommits) => RefreshAllAsync(reloadCommits);

    private async Task RefreshAllAsync(bool reloadCommits)
    {
        if (_disposed) return;
        if (!await _refreshGate.WaitAsync(0))
        {
            _refreshQueued = true;
            return;
        }
        try
        {
            do
            {
                _refreshQueued = false;
                await RefreshCoreAsync(reloadCommits);
                reloadCommits = false;
            } while (_refreshQueued && !_disposed);
        }
        finally { _refreshGate.Release(); }
    }

    private async Task RefreshCoreAsync(bool reloadCommits)
    {
        var statusTask = _git.Status();
        var refsTask = _git.Refs();
        var stashTask = _git.Stashes();
        var opTask = _git.CurrentOperation();
        await Task.WhenAll(statusTask, refsTask, stashTask, opTask);
        if (_disposed) return;

        Snapshot = new RepoSnapshot { Status = statusTask.Result, Refs = refsTask.Result, Stashes = stashTask.Result, Operation = opTask.Result };
        ApplyHeader();
        WorkingTree.Apply(Snapshot.Status);
        History.UpdateWipCount(Snapshot.Status.Staged.Select(f => f.Path).Union(Snapshot.Status.Unstaged.Select(f => f.Path)).Count());
        Sidebar.Build(Snapshot);

        if (reloadCommits || Snapshot.Refs.Signature != _refsSignature)
        {
            _refsSignature = Snapshot.Refs.Signature;
            await History.ReloadAsync();
        }

        // Keep the visible diff in sync with the working tree.
        if (Details.IsWipMode) await Details.ShowWorkingAsync(WorkingTree.SelectedChange);
    }

    private void ApplyHeader()
    {
        var s = Snapshot.Status;
        BranchText = s.IsDetached ? $"HEAD desacoplado ({(s.Oid is { Length: > 7 } o ? o[..7] : "?")})" : s.Branch ?? "";
        var parts = new List<string>();
        if (s.Ahead > 0) parts.Add($"↑{s.Ahead}");
        if (s.Behind > 0) parts.Add($"↓{s.Behind}");
        TrackText = string.Join(' ', parts);

        HasOperation = Snapshot.Operation != RepoOperation.None;
        OperationText = Snapshot.Operation switch
        {
            RepoOperation.Merge => "Merge en curso",
            RepoOperation.Rebase => "Rebase en curso",
            RepoOperation.CherryPick => "Cherry-pick en curso",
            RepoOperation.Revert => "Revert en curso",
            _ => "",
        };
    }

    // ------------------------------------------------------------ IRepoSession

    public void Notify(string text) => Notice = text;

    public async Task Perform(string text, Func<Task<GitResult>> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        BusyText = text;
        try
        {
            var res = await action();
            Notice = res.Ok ? "" : res.Message;
            if (!res.Ok) await _dialogs.Message("Error de git", res.Message);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
        await RefreshAllAsync(true);
    }

    public async Task PerformQuiet(Func<Task<GitResult>> action)
    {
        var res = await action();
        if (!res.Ok) await _dialogs.Message("Error", res.Message);
        await RefreshAllAsync(false);
    }

    public async Task RunRemote(string text, Func<Action<string>, Task<GitResult>> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        BusyText = text + "...";
        try
        {
            var res = await action(chunk =>
            {
                var last = chunk.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()?.Trim();
                if (!string.IsNullOrEmpty(last)) Dispatcher.UIThread.Post(() => BusyText = $"{text}: {last}");
            });
            if (!res.Ok)
            {
                Notice = res.Message;
                await _dialogs.Message($"Error en {text}", res.Message);
            }
            else Notice = $"{text} completado";
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
        await RefreshAllAsync(true);
    }

    // ------------------------------------------------------------ toolbar commands

    [RelayCommand] private Task Fetch() => Actions.Fetch();
    [RelayCommand] private Task Pull() => Actions.Pull(PullMode.Default);
    [RelayCommand] private Task PullRebase() => Actions.Pull(PullMode.Rebase);
    [RelayCommand] private Task PullFastForward() => Actions.Pull(PullMode.FastForwardOnly);
    [RelayCommand] private Task Push() => Actions.Push();
    [RelayCommand] private Task PushForce() => Actions.Push(force: true);
    [RelayCommand] private Task PushToRemote() => Actions.PushToRemote();
    [RelayCommand] private Task PushTags() => Actions.PushTags();
    [RelayCommand] private Task NewBranch() => Actions.NewBranch(null);
    [RelayCommand] private Task Stash() => Actions.Stash();
    [RelayCommand] private Task Pop() => Actions.PopLatest();
    [RelayCommand] private Task AbortOperation() => Actions.Abort();
    [RelayCommand] private Task ContinueOperation() => Actions.Continue();

    [RelayCommand] private void OpenInExplorer() => _shell.OpenFolder(RepoPath);
    [RelayCommand] private void OpenInTerminal() => _shell.OpenTerminal(RepoPath);
    [RelayCommand] private Task OpenInTextPeach() => TextPeachLink.Open(_shell, _dialogs, RepoPath);

    public void Dispose()
    {
        _disposed = true;
        Details.Cancel();
        _watcher.Dispose();
    }
}

public interface IRepoViewModelFactory
{
    RepoViewModel Create(string path);
}

public sealed class RepoViewModelFactory(
    IGitRepositoryFactory repos,
    IRepoWatcherFactory watchers,
    IDialogService dialogs,
    IShellService shell,
    IClipboardService clipboard,
    IWindowService windows) : IRepoViewModelFactory
{
    public RepoViewModel Create(string path)
        => new(path, repos.Open(path), watchers.Watch(path), dialogs, shell, clipboard, windows);
}
