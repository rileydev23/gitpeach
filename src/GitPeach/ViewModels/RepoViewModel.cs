using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Controls;
using GitPeach.Git;
using GitPeach.Models;
using GitPeach.Views;

namespace GitPeach.ViewModels;

public partial class RepoViewModel : TabViewModel, IDisposable
{
    private const int PageSize = 500;
    private const int MaxDiffRows = 20000;

    public string RepoPath { get; }
    public GitRepository Git { get; }

    public ObservableCollection<CommitRowVM> Commits { get; } = new();
    public ObservableCollection<SidebarItem> Sidebar { get; } = new();
    public ObservableCollection<FileEntryVM> Staged { get; } = new();
    public ObservableCollection<FileEntryVM> Unstaged { get; } = new();
    public GraphMetrics Metrics { get; } = new();

    [ObservableProperty] private CommitRowVM? _selectedCommitRow;
    [ObservableProperty] private FileEntryVM? _selectedUnstaged;
    [ObservableProperty] private FileEntryVM? _selectedStaged;
    [ObservableProperty] private CommitFileVM? _selectedCommitFile;
    [ObservableProperty] private bool _isWipMode = true;
    [ObservableProperty] private string _commitMessage = "";
    [ObservableProperty] private bool _amend;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _busyText = "";
    [ObservableProperty] private string _notice = "";
    [ObservableProperty] private string _commandLog = "";
    [ObservableProperty] private bool _showLog;
    [ObservableProperty] private string _branchText = "";
    [ObservableProperty] private string _trackText = "";
    [ObservableProperty] private string _detailMessage = "";
    [ObservableProperty] private string _detailMeta = "";
    [ObservableProperty] private string _operationText = "";
    [ObservableProperty] private bool _hasOperation;
    [ObservableProperty] private bool _hasConflicts;
    // Swapped as a whole (one UI update) instead of cleared and refilled row by row.
    [ObservableProperty] private IReadOnlyList<CommitFileVM> _commitFiles = Array.Empty<CommitFileVM>();
    [ObservableProperty] private IReadOnlyList<DiffRowVM> _diffRows = Array.Empty<DiffRowVM>();

    public event Action<DiffRowVM>? ScrollToRowRequested;
    public event Action? CommitMessageFocusRequested;

    private readonly WipRowHolder _wip;
    private readonly List<string> _hashes = new();
    private GraphLayout _layout = new();
    private int _maxLanes = 2;
    private bool _hasMore = true;
    private bool _loadingMore;
    private string _refsSignature = "";
    private StatusInfo _status = new();
    private RefsSnapshot _refs = new();
    private RepoOperation _operation = RepoOperation.None;
    private CancellationTokenSource? _diffCts;
    private FileSystemWatcher? _watcher;
    private CancellationTokenSource? _debounce;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private bool _refreshQueued;
    private bool _disposed;
    private bool _suppressSelection;

    private sealed record CommitView(List<CommitFileVM> Files, List<DiffRowVM> Rows);
    private readonly Dictionary<string, CommitView> _commitCache = new();
    private readonly Queue<string> _commitCacheOrder = new();
    private const int CommitCacheSize = 30, CommitCacheMaxRows = 8000;
    private static readonly IReadOnlyList<DiffRowVM> LoadingRows = new[]
    {
        new DiffRowVM { Kind = DiffRowKind.Note, Text = "Cargando diff…", Foreground = Palette.Dim },
    };

    private sealed class WipRowHolder { public required CommitRowVM Row { get; init; } }

    public RepoViewModel(string path)
    {
        RepoPath = path;
        Git = new GitRepository(path);
        Title = System.IO.Path.GetFileName(path.TrimEnd('\\', '/'));
        Git.CommandLogged += text => Dispatcher.UIThread.Post(() =>
        {
            CommandLog += (CommandLog.Length > 0 ? "\n\n" : "") + text;
            if (CommandLog.Length > 60000) CommandLog = CommandLog[^40000..];
        });

        _wip = new WipRowHolder
        {
            Row = new CommitRowVM { IsWip = true, Metrics = Metrics, Subject = "Working Tree" },
        };
        Commits.Add(_wip.Row);
        SelectedCommitRow = _wip.Row;
        StartWatcher();
        _ = RefreshAllAsync(reloadCommits: true);
    }

    // ------------------------------------------------------------ watcher

    private void StartWatcher()
    {
        try
        {
            _watcher = new FileSystemWatcher(RepoPath)
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
            _watcher.Error += (_, _) => ScheduleRefresh();
            _watcher.EnableRaisingEvents = true;
        }
        catch { /* no watching; manual refresh still works */ }
    }

    private void OnFsEvent(string fullPath)
    {
        var rel = fullPath.Length > RepoPath.Length ? fullPath[RepoPath.Length..] : "";
        if (rel.Contains(@"\.git\objects", StringComparison.OrdinalIgnoreCase) ||
            rel.Contains(@"\.git\lfs", StringComparison.OrdinalIgnoreCase) ||
            rel.EndsWith(".lock", StringComparison.OrdinalIgnoreCase))
            return;
        ScheduleRefresh();
    }

    private void ScheduleRefresh()
    {
        if (_disposed) return;
        _debounce?.Cancel();
        var cts = _debounce = new CancellationTokenSource();
        _ = Task.Delay(400, cts.Token).ContinueWith(t =>
        {
            if (t.IsCanceled || _disposed) return;
            Dispatcher.UIThread.Post(() => _ = RefreshAllAsync(reloadCommits: false));
        }, TaskScheduler.Default);
    }

    // ------------------------------------------------------------ refresh

    [RelayCommand]
    public Task RefreshAsync() => RefreshAllAsync(reloadCommits: true);

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
        var statusTask = Git.Status();
        var refsTask = Git.Refs();
        var stashTask = Git.Stashes();
        var opTask = Git.CurrentOperation();
        await Task.WhenAll(statusTask, refsTask, stashTask, opTask);
        if (_disposed) return;

        _status = statusTask.Result;
        _refs = refsTask.Result;
        _operation = opTask.Result;

        ApplyStatus();
        BuildSidebar(stashTask.Result);

        if (reloadCommits || _refs.Signature != _refsSignature)
        {
            _refsSignature = _refs.Signature;
            await LoadCommitsAsync();
        }
        else
        {
            // Commit decorations/WIP may have changed even if the refs are equal; the WIP row is already updated.
        }

        // Keep the visible diff in sync with the working tree.
        if (IsWipMode) await LoadWipDiffAsync();
    }

    private void ApplyStatus()
    {
        BranchText = _status.IsDetached ? $"HEAD desacoplado ({(_status.Oid is { Length: > 7 } o ? o[..7] : "?")})" : _status.Branch ?? "";
        var parts = new List<string>();
        if (_status.Ahead > 0) parts.Add($"↑{_status.Ahead}");
        if (_status.Behind > 0) parts.Add($"↓{_status.Behind}");
        TrackText = string.Join(' ', parts);

        HasOperation = _operation != RepoOperation.None;
        OperationText = _operation switch
        {
            RepoOperation.Merge => "Merge en curso",
            RepoOperation.Rebase => "Rebase en curso",
            RepoOperation.CherryPick => "Cherry-pick en curso",
            RepoOperation.Revert => "Revert en curso",
            _ => "",
        };
        HasConflicts = _status.Unstaged.Any(f => f.IsConflict);

        var selUn = SelectedUnstaged?.Path;
        var selSt = SelectedStaged?.Path;

        _suppressSelection = true;
        Sync(Unstaged, _status.Unstaged);
        Sync(Staged, _status.Staged);
        SelectedUnstaged = selUn is null ? null : Unstaged.FirstOrDefault(f => f.Path == selUn);
        SelectedStaged = selSt is null ? null : Staged.FirstOrDefault(f => f.Path == selSt);
        _suppressSelection = false;

        int count = _status.Staged.Select(f => f.Path).Union(_status.Unstaged.Select(f => f.Path)).Count();
        _wip.Row.Subject = count == 0 ? "Working Tree (limpio)" : $"Working Tree ({count} cambio{(count == 1 ? "" : "s")})";
    }

    private void Sync(ObservableCollection<FileEntryVM> target, List<FileChange> source)
    {
        // Rebuild only when the list actually differs (avoids flicker and selection loss).
        bool same = target.Count == source.Count;
        if (same)
            for (int i = 0; i < source.Count; i++)
                if (target[i].Change != source[i]) { same = false; break; }
        if (same) return;

        target.Clear();
        foreach (var c in source.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase))
        {
            var change = c;
            target.Add(new FileEntryVM
            {
                Change = change,
                PrimaryCommand = new AsyncRelayCommand(() => change.Staged ? UnstagePaths(new[] { change.Path }) : StagePaths(new[] { change.Path })),
                DiscardCommand = new AsyncRelayCommand(() => DiscardFile(change)),
            });
        }
    }

    // ------------------------------------------------------------ commits

    private async Task LoadCommitsAsync()
    {
        var want = Math.Max(_hashes.Count, PageSize);
        var list = await Git.Log(0, want);
        if (_disposed) return;
        _hasMore = list.Count >= want;

        int common = 0;
        while (common < _hashes.Count && common < list.Count && _hashes[common] == list[common].Hash) common++;

        string? keepHash = SelectedCommitRow?.Commit?.Hash;
        bool wasWip = SelectedCommitRow?.IsWip ?? true;

        IEnumerable<Commit> toAdd;
        if (common == _hashes.Count && _hashes.Count > 0)
        {
            toAdd = list.Skip(common);
        }
        else
        {
            // History diverged (new commit, rebase, ...): rebuild everything.
            _suppressSelection = true;
            while (Commits.Count > 1) Commits.RemoveAt(Commits.Count - 1);
            _hashes.Clear();
            _layout = new GraphLayout();
            _maxLanes = 2;
            _suppressSelection = false;
            toAdd = list;
        }

        AppendCommits(toAdd.ToList());

        // Restore selection after a rebuild.
        if (SelectedCommitRow is null || !Commits.Contains(SelectedCommitRow))
        {
            SelectedCommitRow = (!wasWip && keepHash is not null
                ? Commits.FirstOrDefault(r => r.Commit?.Hash == keepHash) : null) ?? _wip.Row;
        }
    }

    private void AppendCommits(IReadOnlyList<Commit> commits)
    {
        if (commits.Count == 0) return;
        var rows = _layout.Append(commits);
        for (int i = 0; i < commits.Count; i++)
        {
            var c = commits[i];
            _maxLanes = Math.Max(_maxLanes, rows[i].Width);
            _hashes.Add(c.Hash);
            Commits.Add(new CommitRowVM
            {
                Commit = c,
                Graph = rows[i],
                Metrics = Metrics,
                Subject = c.Subject,
                Author = c.Author,
                ShortHash = c.ShortHash,
                DateText = c.Date.ToString("yyyy-MM-dd HH:mm"),
                Badges = c.Refs.Select(r => new RefBadge
                {
                    Text = r.Name,
                    Background = r.Kind switch
                    {
                        RefKind.Tag => Palette.RefTag,
                        RefKind.RemoteBranch => Palette.RefRemote,
                        RefKind.Head => Palette.RefHead,
                        _ => r.IsCurrent ? Palette.Accent : Palette.RefLocal,
                    },
                }).ToList(),
            });
        }
        Metrics.Update(_maxLanes);
    }

    public async Task LoadMoreAsync()
    {
        if (_loadingMore || !_hasMore || _disposed) return;
        _loadingMore = true;
        try
        {
            var list = await Git.Log(_hashes.Count, PageSize);
            if (_disposed) return;
            _hasMore = list.Count >= PageSize;
            AppendCommits(list);
        }
        finally { _loadingMore = false; }
    }

    // ------------------------------------------------------------ selection / diffs

    partial void OnSelectedCommitRowChanged(CommitRowVM? value)
    {
        if (_suppressSelection || value is null) return;
        if (value.IsWip)
        {
            IsWipMode = true;
            _ = LoadWipDiffAsync();
        }
        else
        {
            IsWipMode = false;
            _ = LoadCommitAsync(value.Commit!);
        }
    }

    partial void OnSelectedUnstagedChanged(FileEntryVM? value)
    {
        if (_suppressSelection) return;
        if (value is not null)
        {
            _suppressSelection = true;
            SelectedStaged = null;
            _suppressSelection = false;
        }
        _ = LoadWipDiffAsync();
    }

    partial void OnSelectedStagedChanged(FileEntryVM? value)
    {
        if (_suppressSelection) return;
        if (value is not null)
        {
            _suppressSelection = true;
            SelectedUnstaged = null;
            _suppressSelection = false;
        }
        _ = LoadWipDiffAsync();
    }

    partial void OnSelectedCommitFileChanged(CommitFileVM? value)
    {
        if (value is null) return;
        var row = DiffRows.FirstOrDefault(r => r.IsFileHeader && r.File?.DisplayPath == value.Path);
        if (row is not null) ScrollToRowRequested?.Invoke(row);
    }

    private CancellationToken NewDiffToken()
    {
        _diffCts?.Cancel();
        _diffCts = new CancellationTokenSource();
        return _diffCts.Token;
    }

    /// <summary>
    /// Header is shown instantly from data the log already fetched (no git process). Files and diff
    /// load independently, in parallel, and each one appears as soon as it is ready.
    /// </summary>
    private async Task LoadCommitAsync(Commit commit)
    {
        var ct = NewDiffToken();
        ShowCommitHeader(commit);
        SelectedCommitFile = null;

        if (_commitCache.TryGetValue(commit.Hash, out var hit))
        {
            CommitFiles = hit.Files;
            DiffRows = hit.Rows;
            return;
        }
        CommitFiles = Array.Empty<CommitFileVM>();
        DiffRows = LoadingRows;

        try
        {
            // Holding the arrow key shouldn't spawn git for every row we fly past.
            await Task.Delay(60, ct);

            var filesTask = Git.CommitFiles(commit.Hash, ct);
            var diffTask = Task.Run(async () =>
            {
                var diff = await Git.CommitDiff(commit.Hash, ct);
                var rows = BuildRows(diff, wipMode: false, staged: false, untracked: false);
                return (diff, rows);
            }, ct);

            var entries = await filesTask;
            if (ct.IsCancellationRequested) return;
            var files = entries.Select(e => new CommitFileVM { Path = e.Path, Status = e.Status }).ToList();
            CommitFiles = files;

            var (diffFiles, diffRows) = await diffTask;
            if (ct.IsCancellationRequested) return;
            if (files.Count == 0)
            {
                files = diffFiles.Select(f => new CommitFileVM
                {
                    Path = f.DisplayPath,
                    Status = f.IsNew ? 'A' : f.IsDeleted ? 'D' : f.OldPath is not null && f.NewPath is not null && f.OldPath != f.NewPath ? 'R' : 'M',
                }).ToList();
                CommitFiles = files;
            }
            DiffRows = diffRows;
            Remember(commit.Hash, files, diffRows);
        }
        catch (OperationCanceledException) { }
    }

    private void ShowCommitHeader(Commit c)
    {
        DetailMessage = string.IsNullOrWhiteSpace(c.Message) ? c.Subject : c.Message;
        var sb = new StringBuilder();
        sb.AppendLine($"Commit  {c.Hash}");
        if (c.Parents.Length > 0) sb.AppendLine($"Padres  {string.Join(' ', c.Parents.Select(p => p[..Math.Min(8, p.Length)]))}");
        sb.AppendLine($"Autor   {c.Author} <{c.Email}>");
        sb.Append($"Fecha   {c.Date:yyyy-MM-dd HH:mm:ss}");
        if (c.Committer.Length > 0 && c.Committer != c.Author)
            sb.Append($"\nCommitter {c.Committer} <{c.CommitterEmail}>");
        DetailMeta = sb.ToString();
    }

    private void Remember(string hash, List<CommitFileVM> files, List<DiffRowVM> rows)
    {
        if (rows.Count > CommitCacheMaxRows || _commitCache.ContainsKey(hash)) return;
        _commitCache[hash] = new CommitView(files, rows);
        _commitCacheOrder.Enqueue(hash);
        while (_commitCacheOrder.Count > CommitCacheSize)
            _commitCache.Remove(_commitCacheOrder.Dequeue());
    }

    private async Task LoadWipDiffAsync()
    {
        if (!IsWipMode) return;
        var sel = SelectedUnstaged ?? SelectedStaged;
        var ct = NewDiffToken();
        if (sel is null)
        {
            DiffRows = Array.Empty<DiffRowVM>();
            return;
        }
        try
        {
            var files = await Git.WorkingDiff(sel.Change, ct);
            if (ct.IsCancellationRequested) return;
            var staged = sel.Staged;
            var untracked = sel.Change.IsUntracked;
            var rows = await Task.Run(() => BuildRows(files, wipMode: true, staged: staged, untracked: untracked), ct);
            if (ct.IsCancellationRequested) return;
            DiffRows = rows;
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Pure (no UI access), so it can run on a background thread.</summary>
    private List<DiffRowVM> BuildRows(List<DiffFile> files, bool wipMode, bool staged, bool untracked)
    {
        var rows = new List<DiffRowVM>();
        foreach (var file in files)
        {
            var title = file.DisplayPath;
            if (file.OldPath is not null && file.NewPath is not null && file.OldPath != file.NewPath)
                title = $"{file.OldPath} → {file.NewPath}";
            rows.Add(new DiffRowVM { Kind = DiffRowKind.FileHeader, Text = title, File = file, Background = Palette.FileBg, Foreground = Palette.Text });

            if (file.IsBinary)
            {
                rows.Add(new DiffRowVM { Kind = DiffRowKind.Note, Text = "Archivo binario no mostrado", Foreground = Palette.Dim });
                continue;
            }
            if (file.Hunks.Count == 0)
            {
                rows.Add(new DiffRowVM { Kind = DiffRowKind.Note, Text = file.IsNew ? "Archivo vacío" : "Sin cambios de contenido (permisos o renombrado)", Foreground = Palette.Dim });
                continue;
            }

            foreach (var hunk in file.Hunks)
            {
                var f = file; var h = hunk;
                // Hunk commands only exist in working-tree mode; skip allocating them for commit diffs.
                rows.Add(new DiffRowVM
                {
                    Kind = DiffRowKind.HunkHeader,
                    Text = hunk.Header,
                    File = file, Hunk = hunk,
                    Background = Palette.HunkBg, Foreground = Palette.Dim,
                    CanStage = wipMode && !staged && !untracked,
                    CanUnstage = wipMode && staged,
                    CanDiscard = wipMode && !staged && !untracked,
                    StageCommand = wipMode ? new AsyncRelayCommand(() => HunkAction(() => Git.StageHunk(f, h))) : null,
                    UnstageCommand = wipMode ? new AsyncRelayCommand(() => HunkAction(() => Git.UnstageHunk(f, h))) : null,
                    DiscardCommand = wipMode ? new AsyncRelayCommand(async () =>
                    {
                        if (await Dialogs.Confirm("Descartar hunk", "¿Descartar estos cambios? Esta acción no se puede deshacer.", "Descartar"))
                            await HunkAction(() => Git.DiscardHunk(f, h));
                    }) : null,
                });
                foreach (var l in hunk.Lines)
                {
                    rows.Add(new DiffRowVM
                    {
                        Kind = DiffRowKind.Line,
                        Text = l.Text,
                        Prefix = l.Prefix,
                        OldNo = l.OldNo > 0 ? l.OldNo.ToString() : "",
                        NewNo = l.NewNo > 0 ? l.NewNo.ToString() : "",
                        Background = l.Kind switch { DiffLineKind.Added => Palette.AddedBg, DiffLineKind.Removed => Palette.RemovedBg, _ => Palette.Transparent },
                        Foreground = l.Kind switch { DiffLineKind.Added => Palette.AddedFg, DiffLineKind.Removed => Palette.RemovedFg, DiffLineKind.NoNewline => Palette.Dim, _ => Palette.ContextFg },
                    });
                }
                if (rows.Count > MaxDiffRows)
                {
                    rows.Add(new DiffRowVM { Kind = DiffRowKind.Note, Text = $"Diff truncado a {MaxDiffRows} líneas", Foreground = Palette.Dim });
                    return rows;
                }
            }
        }
        return rows;
    }

    private async Task HunkAction(Func<Task<GitResult>> action)
    {
        var res = await action();
        if (!res.Ok) await Dialogs.Message("Error", res.Message);
        await RefreshAllAsync(false);
    }

    // ------------------------------------------------------------ sidebar

    private void BuildSidebar(List<StashInfo> stashes)
    {
        Sidebar.Clear();
        Sidebar.Add(new SidebarItem { Kind = SidebarKind.Header, Text = "RAMAS" });
        foreach (var b in _refs.Locals.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
            Sidebar.Add(new SidebarItem
            {
                Kind = SidebarKind.LocalBranch, Text = b.Name, IsCurrent = b.IsCurrent, Indent = 12, Payload = b,
                Detail = (b.IsCurrent ? "● " : "") + (b.Track ?? "").Trim('[', ']').Replace("ahead ", "↑").Replace("behind ", "↓").Replace(", ", " "),
            });

        Sidebar.Add(new SidebarItem { Kind = SidebarKind.Header, Text = "REMOTOS" });
        foreach (var remote in _refs.RemoteNames)
        {
            Sidebar.Add(new SidebarItem { Kind = SidebarKind.Header, Text = remote, Indent = 12 });
            foreach (var b in _refs.Remotes.Where(b => b.Remote == remote).OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
                Sidebar.Add(new SidebarItem { Kind = SidebarKind.RemoteBranch, Text = b.ShortRemoteName, Indent = 24, Payload = b });
        }

        if (_refs.Tags.Count > 0)
        {
            Sidebar.Add(new SidebarItem { Kind = SidebarKind.Header, Text = "TAGS" });
            foreach (var t in _refs.Tags.OrderByDescending(t => t.Name, StringComparer.OrdinalIgnoreCase))
                Sidebar.Add(new SidebarItem { Kind = SidebarKind.Tag, Text = t.Name, Indent = 12, Payload = t });
        }

        if (stashes.Count > 0)
        {
            Sidebar.Add(new SidebarItem { Kind = SidebarKind.Header, Text = "STASHES" });
            foreach (var s in stashes)
                Sidebar.Add(new SidebarItem { Kind = SidebarKind.Stash, Text = s.Message, Detail = s.Name, Indent = 12, Payload = s });
        }
    }

    /// <summary>Double-click on a sidebar entry.</summary>
    public async Task ActivateAsync(SidebarItem item)
    {
        switch (item.Kind)
        {
            case SidebarKind.LocalBranch when item.Payload is BranchInfo { IsCurrent: false } b:
                await Perform($"checkout {b.Name}", () => Git.Checkout(b.Name));
                break;
            case SidebarKind.RemoteBranch when item.Payload is BranchInfo rb:
                await CheckoutRemoteAsync(rb);
                break;
            case SidebarKind.LocalBranch or SidebarKind.Tag or SidebarKind.RemoteBranch:
                SelectCommitFor(item);
                break;
        }
    }

    private void SelectCommitFor(SidebarItem item)
    {
        var hash = item.Payload switch { BranchInfo b => b.Hash, TagInfo t => t.Hash, _ => null };
        var row = hash is null ? null : Commits.FirstOrDefault(r => r.Commit?.Hash == hash);
        if (row is not null) SelectedCommitRow = row;
    }

    private async Task CheckoutRemoteAsync(BranchInfo rb)
    {
        var local = _refs.Locals.FirstOrDefault(l => l.Name == rb.ShortRemoteName);
        if (local is not null)
            await Perform($"checkout {local.Name}", () => Git.Checkout(local.Name));
        else
            await Perform($"checkout {rb.ShortRemoteName}", () => Git.CheckoutRemote(rb.Name, rb.ShortRemoteName));
    }

    public IReadOnlyList<(string Header, Func<Task> Action)> ActionsFor(SidebarItem item)
    {
        var list = new List<(string, Func<Task>)>();
        switch (item.Payload)
        {
            case BranchInfo { IsRemote: false } b:
                if (!b.IsCurrent)
                {
                    list.Add(("Checkout", () => Perform($"checkout {b.Name}", () => Git.Checkout(b.Name))));
                    list.Add(("Merge en la rama actual", () => Perform($"merge {b.Name}", () => Git.Merge(b.Name))));
                    list.Add(("Rebase de la rama actual sobre esta", () => Perform($"rebase {b.Name}", () => Git.Rebase(b.Name))));
                }
                list.Add(("Nueva rama desde aquí...", () => NewBranchFrom(b.Name)));
                list.Add(("Renombrar...", async () =>
                {
                    var n = await Dialogs.Prompt("Renombrar rama", "Nuevo nombre", b.Name);
                    if (!string.IsNullOrWhiteSpace(n) && n != b.Name) await Perform("rename", () => Git.RenameBranch(b.Name, n.Trim()));
                }));
                list.Add(("Push", () => PushBranch(b)));
                if (!b.IsCurrent)
                    list.Add(("Eliminar rama", async () =>
                    {
                        if (await Dialogs.Confirm("Eliminar rama", $"¿Eliminar la rama '{b.Name}'?", "Eliminar"))
                        {
                            var r = await Git.DeleteBranch(b.Name, false);
                            if (!r.Ok && await Dialogs.Confirm("La rama no está fusionada", r.Message + "\n\n¿Forzar eliminación?", "Forzar"))
                                r = await Git.DeleteBranch(b.Name, true);
                            if (!r.Ok) await Dialogs.Message("Error", r.Message);
                            await RefreshAllAsync(true);
                        }
                    }));
                break;
            case BranchInfo { IsRemote: true } rb:
                list.Add(("Checkout (crear rama local)", () => CheckoutRemoteAsync(rb)));
                list.Add(("Merge en la rama actual", () => Perform($"merge {rb.Name}", () => Git.Merge(rb.Name))));
                list.Add(("Nueva rama desde aquí...", () => NewBranchFrom(rb.Name)));
                list.Add(("Eliminar rama remota", async () =>
                {
                    if (await Dialogs.Confirm("Eliminar rama remota", $"¿Eliminar '{rb.ShortRemoteName}' de '{rb.Remote}'?", "Eliminar"))
                        await Perform("delete remote branch", () => Git.DeleteRemoteBranch(rb.Remote!, rb.ShortRemoteName));
                }));
                break;
            case TagInfo t:
                list.Add(("Checkout", () => Perform($"checkout {t.Name}", () => Git.Checkout(t.Name))));
                list.Add(("Eliminar tag", async () =>
                {
                    if (await Dialogs.Confirm("Eliminar tag", $"¿Eliminar el tag '{t.Name}'?", "Eliminar"))
                        await Perform("delete tag", () => Git.DeleteTag(t.Name));
                }));
                break;
            case StashInfo s:
                list.Add(("Pop", () => Perform("stash pop", () => Git.StashPop(s.Name))));
                list.Add(("Apply", () => Perform("stash apply", () => Git.StashApply(s.Name))));
                list.Add(("Drop", async () =>
                {
                    if (await Dialogs.Confirm("Eliminar stash", $"¿Eliminar {s.Name}?", "Eliminar"))
                        await Perform("stash drop", () => Git.StashDrop(s.Name));
                }));
                break;
        }
        return list;
    }

    public IReadOnlyList<(string Header, Func<Task> Action)> ActionsFor(CommitRowVM row)
    {
        var list = new List<(string, Func<Task>)>();
        if (row.Commit is not { } c) return list;
        list.Add(("Copiar hash", async () =>
        {
            var top = Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime l ? l.MainWindow : null;
            if (top?.Clipboard is { } cb) await cb.SetTextAsync(c.Hash);
        }));
        list.Add(("Checkout (HEAD desacoplado)", () => Perform("checkout", () => Git.Checkout(c.Hash))));
        list.Add(("Nueva rama aquí...", () => NewBranchFrom(c.Hash)));
        list.Add(("Nuevo tag aquí...", async () =>
        {
            var n = await Dialogs.Prompt("Nuevo tag", "Nombre del tag");
            if (!string.IsNullOrWhiteSpace(n)) await Perform("tag", () => Git.CreateTag(n.Trim(), c.Hash));
        }));
        list.Add(("Cherry-pick", () => Perform("cherry-pick", () => Git.CherryPick(c.Hash))));
        list.Add(("Revert", () => Perform("revert", () => Git.Revert(c.Hash))));
        list.Add(("Reset suave (soft) hasta aquí", () => ResetTo(c, "soft", false)));
        list.Add(("Reset mixto (mixed) hasta aquí", () => ResetTo(c, "mixed", false)));
        list.Add(("Reset duro (hard) hasta aquí", () => ResetTo(c, "hard", true)));
        return list;
    }

    private async Task ResetTo(Commit c, string mode, bool confirm)
    {
        if (confirm && !await Dialogs.Confirm("Reset hard", $"Se perderán los cambios sin commit y se moverá la rama a {c.ShortHash}. ¿Continuar?", "Reset hard")) return;
        await Perform($"reset --{mode}", () => Git.ResetTo(c.Hash, mode));
    }

    private async Task NewBranchFrom(string start)
    {
        var n = await Dialogs.Prompt("Nueva rama", "Nombre de la rama");
        if (!string.IsNullOrWhiteSpace(n)) await Perform("new branch", () => Git.CreateBranch(n.Trim(), start, true));
    }

    private async Task PushBranch(BranchInfo b)
    {
        var remote = _refs.RemoteNames.FirstOrDefault();
        if (b.Upstream is null && remote is null)
        {
            await Dialogs.Message("Push", "No hay remotos configurados.");
            return;
        }
        await RunRemote("push", p => b.Upstream is null
            ? Git.Push(true, remote, b.Name, false, p)
            : Git.Push(false, null, null, false, p));
    }

    // ------------------------------------------------------------ operations

    /// <summary>Runs a git action, shows errors, then refreshes everything.</summary>
    private async Task Perform(string text, Func<Task<GitResult>> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        BusyText = text;
        try
        {
            var res = await action();
            Notice = res.Ok ? "" : res.Message;
            if (!res.Ok) await Dialogs.Message("Error de git", res.Message);
        }
        finally
        {
            IsBusy = false;
            BusyText = "";
        }
        await RefreshAllAsync(true);
    }

    private async Task RunRemote(string text, Func<Action<string>, Task<GitResult>> action)
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
                await Dialogs.Message($"Error en {text}", res.Message);
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

    [RelayCommand] private Task Fetch() => RunRemote("fetch", p => Git.Fetch(p));
    [RelayCommand] private Task Pull() => RunRemote("pull", p => Git.Pull(p));

    [RelayCommand]
    private async Task Push()
    {
        if (_status.IsDetached)
        {
            await Dialogs.Message("Push", "No se puede hacer push con HEAD desacoplado.");
            return;
        }
        var remote = _refs.RemoteNames.FirstOrDefault();
        if (_status.Upstream is null)
        {
            if (remote is null) { await Dialogs.Message("Push", "No hay remotos configurados."); return; }
            await RunRemote("push", p => Git.Push(true, remote, _status.Branch, false, p));
        }
        else await RunRemote("push", p => Git.Push(false, null, null, false, p));
    }

    [RelayCommand]
    private async Task NewBranch()
    {
        var n = await Dialogs.Prompt("Nueva rama", "Nombre de la rama (desde HEAD)");
        if (!string.IsNullOrWhiteSpace(n)) await Perform("new branch", () => Git.CreateBranch(n.Trim(), null, true));
    }

    [RelayCommand]
    private async Task Stash()
    {
        var m = await Dialogs.Prompt("Stash", "Mensaje (opcional)");
        if (m is null) return;
        await Perform("stash", () => Git.StashPush(m));
    }

    [RelayCommand]
    private async Task Pop()
    {
        var stashes = await Git.Stashes();
        if (stashes.Count == 0) { Notice = "No hay stashes"; return; }
        await Perform("stash pop", () => Git.StashPop(stashes[0].Name));
    }

    [RelayCommand]
    private async Task AbortOperation()
        => await Perform("abort", () => Git.Abort(_operation));

    [RelayCommand]
    private async Task ContinueOperation()
        => await Perform("continue", () => Git.Continue(_operation));

    [RelayCommand] private void ToggleLog() => ShowLog = !ShowLog;
    [RelayCommand] private void ClearLog() => CommandLog = "";

    // ------------------------------------------------------------ staging

    private async Task StagePaths(IEnumerable<string> paths)
    {
        var res = await Git.Stage(paths);
        if (!res.Ok) await Dialogs.Message("Error", res.Message);
        await RefreshAllAsync(false);
    }

    private async Task UnstagePaths(IEnumerable<string> paths)
    {
        var res = await Git.Unstage(paths);
        if (!res.Ok) await Dialogs.Message("Error", res.Message);
        await RefreshAllAsync(false);
    }

    private async Task DiscardFile(FileChange change)
    {
        var what = change.IsUntracked ? "Se eliminará el archivo sin seguimiento" : "Se perderán los cambios";
        if (!await Dialogs.Confirm("Descartar cambios", $"{what}: {change.Path}\n\nEsta acción no se puede deshacer.", "Descartar")) return;
        var res = await Git.Discard(change);
        if (!res.Ok) await Dialogs.Message("Error", res.Message);
        await RefreshAllAsync(false);
    }

    [RelayCommand]
    private Task StageAll() => Unstaged.Count == 0 ? Task.CompletedTask : StagePaths(new[] { "." });

    [RelayCommand]
    private Task UnstageAll() => Staged.Count == 0 ? Task.CompletedTask : UnstagePaths(new[] { "." });

    partial void OnAmendChanged(bool value)
    {
        if (value && string.IsNullOrWhiteSpace(CommitMessage))
            _ = LoadAmendMessageAsync();
    }

    private async Task LoadAmendMessageAsync() => CommitMessage = await Git.LastCommitMessage();

    [RelayCommand]
    private async Task CommitChanges()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            Notice = "Escribe un mensaje de commit";
            CommitMessageFocusRequested?.Invoke();
            return;
        }
        if (Staged.Count == 0 && !Amend)
        {
            Notice = "No hay cambios en stage";
            return;
        }
        if (HasConflicts)
        {
            Notice = "Resuelve los conflictos antes de hacer commit";
            return;
        }
        var msg = CommitMessage;
        var amend = Amend;
        if (IsBusy) return;
        IsBusy = true;
        BusyText = "commit";
        try
        {
            var res = await Git.Commit(msg, amend);
            if (res.Ok)
            {
                CommitMessage = "";
                Amend = false;
                Notice = "Commit creado";
            }
            else
            {
                Notice = res.Message;
                await Dialogs.Message("Error al hacer commit", res.Message);
            }
        }
        finally { IsBusy = false; BusyText = ""; }
        await RefreshAllAsync(true);
    }

    // ------------------------------------------------------------ external

    [RelayCommand]
    private void OpenInExplorer()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{RepoPath}\"") { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenInTerminal()
    {
        try { Process.Start(new ProcessStartInfo("cmd.exe") { WorkingDirectory = RepoPath, UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenInVsCode()
    {
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", $"/c code \"{RepoPath}\"") { CreateNoWindow = true, UseShellExecute = false });
        }
        catch { }
    }

    public void Dispose()
    {
        _disposed = true;
        _debounce?.Cancel();
        _diffCts?.Cancel();
        _watcher?.Dispose();
    }
}
