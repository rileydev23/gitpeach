using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Git;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>Right-hand pane: header and file list of the selected commit, and the diff below.</summary>
public sealed partial class DetailsViewModel : ObservableObject
{
    private const int CacheSize = 30, CacheMaxRows = 8000;

    private readonly IGitQueries _git;
    private readonly IHunkActions _hunkActions;
    private CancellationTokenSource? _cts;

    private sealed record CommitView(List<CommitFileVM> Files, List<DiffRowVM> Rows);
    private readonly Dictionary<string, CommitView> _cache = new();
    private readonly Queue<string> _cacheOrder = new();

    public string Title { get; }
    public DiffPaneViewModel Diff { get; }

    [ObservableProperty] private bool _isWipMode = true;
    [ObservableProperty] private Commit? _currentCommit;
    [ObservableProperty] private string _detailMessage = "";
    [ObservableProperty] private string _detailMeta = "";
    [ObservableProperty] private IReadOnlyList<CommitFileVM> _commitFiles = Array.Empty<CommitFileVM>();
    [ObservableProperty] private CommitFileVM? _selectedCommitFile;

    public DetailsViewModel(string title, IGitQueries git, IHunkActions hunkActions, IWindowService windows)
    {
        Title = title;
        _git = git;
        _hunkActions = hunkActions;
        Diff = new DiffPaneViewModel { PopOutCommand = new RelayCommand(() => windows.Show(this)) };
    }

    partial void OnSelectedCommitFileChanged(CommitFileVM? value)
    {
        if (value is not null) Diff.ScrollToFile(value.Path);
    }

    private CancellationToken NewToken()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        return _cts.Token;
    }

    /// <summary>
    /// Header is shown instantly from data the log already fetched (no git process). Files and diff
    /// load independently, in parallel, and each one appears as soon as it is ready.
    /// </summary>
    public async Task ShowCommitAsync(Commit commit)
    {
        IsWipMode = false;
        var ct = NewToken();
        CurrentCommit = commit;
        ShowHeader(commit);
        SelectedCommitFile = null;

        if (_cache.TryGetValue(commit.Hash, out var hit))
        {
            CommitFiles = hit.Files;
            Diff.Rows = hit.Rows;
            return;
        }
        CommitFiles = Array.Empty<CommitFileVM>();
        Diff.Rows = DiffPaneViewModel.Loading;

        try
        {
            // Holding the arrow key shouldn't spawn git for every row we fly past.
            await Task.Delay(60, ct);

            var filesTask = _git.CommitFiles(commit.Hash, ct);
            var diffTask = Task.Run(async () =>
            {
                var diff = await _git.CommitDiff(commit.Hash, ct);
                return (diff, rows: DiffRowBuilder.Build(diff));
            }, ct);

            var entries = await filesTask;
            if (ct.IsCancellationRequested) return;
            var files = entries.Select(e => new CommitFileVM { Path = e.Path, Status = e.Status }).ToList();
            CommitFiles = files;

            var (diffFiles, rows) = await diffTask;
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
            Diff.Rows = rows;
            Remember(commit.Hash, files, rows);
        }
        catch (OperationCanceledException) { }
    }

    /// <summary>Shows the working-tree diff of one file (or nothing when no file is selected).</summary>
    public async Task ShowWorkingAsync(FileChange? change)
    {
        IsWipMode = true;
        CurrentCommit = null;
        var ct = NewToken();
        if (change is null)
        {
            Diff.Clear();
            return;
        }
        try
        {
            var files = await _git.WorkingDiff(change, ct);
            if (ct.IsCancellationRequested) return;
            var rows = await Task.Run(() => DiffRowBuilder.Build(files, _hunkActions, change.Staged, change.IsUntracked), ct);
            if (ct.IsCancellationRequested) return;
            Diff.Rows = rows;
        }
        catch (OperationCanceledException) { }
    }

    private void ShowHeader(Commit c)
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
        if (rows.Count > CacheMaxRows || _cache.ContainsKey(hash)) return;
        _cache[hash] = new CommitView(files, rows);
        _cacheOrder.Enqueue(hash);
        while (_cacheOrder.Count > CacheSize)
            _cache.Remove(_cacheOrder.Dequeue());
    }

    public void Cancel() => _cts?.Cancel();
}
