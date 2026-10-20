using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Controls;
using GitPeach.Git;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>The commit list: graph, paging, selection, search and per-commit actions.</summary>
public sealed partial class HistoryViewModel : ObservableObject
{
    private const int PageSize = 500;
    private const int SearchLimit = 1000;

    private readonly IGitQueries _git;
    private readonly RepoActions _actions;
    private readonly IClipboardService _clipboard;

    private readonly List<string> _hashes = new();
    private GraphLayout _layout = new();
    private int _maxLanes = 2;
    private bool _hasMore = true;
    private bool _loadingMore;
    private bool _suppressSelection;
    private CancellationTokenSource? _searchCts;

    public ObservableCollection<CommitRowVM> Commits { get; } = new();
    public GraphMetrics Metrics { get; } = new();
    public CommitRowVM WipRow { get; }

    public static IReadOnlyList<string> SearchModes { get; } = new[] { "Mensaje", "Autor", "Hash", "Código (-S)" };

    [ObservableProperty] private CommitRowVM? _selectedCommitRow;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private int _searchModeIndex;
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private string _searchStatus = "";

    /// <summary>Raised when the user selects a row (not for programmatic list rebuilds).</summary>
    public event Action<CommitRowVM>? SelectionChanged;
    public event Action? FocusSearchRequested;

    public HistoryViewModel(IGitQueries git, RepoActions actions, IClipboardService clipboard)
    {
        _git = git;
        _actions = actions;
        _clipboard = clipboard;
        WipRow = new CommitRowVM { IsWip = true, Metrics = Metrics, Subject = "Working Tree" };
        Commits.Add(WipRow);
        _selectedCommitRow = WipRow;
    }

    private LogQuery Query => new(SearchText, (SearchMode)SearchModeIndex);

    partial void OnSelectedCommitRowChanged(CommitRowVM? value)
    {
        if (!_suppressSelection && value is not null) SelectionChanged?.Invoke(value);
    }

    public void UpdateWipCount(int count)
        => WipRow.Subject = count == 0 ? "Working Tree (limpio)" : $"Working Tree ({count} cambio{(count == 1 ? "" : "s")})";

    // ---------- loading ----------

    /// <summary>Reloads the list, keeping already-shown rows when history only grew (no flicker).</summary>
    public async Task ReloadAsync()
    {
        if (IsSearching) { await RunSearchAsync(); return; }

        var want = Math.Max(_hashes.Count, PageSize);
        var list = await _git.Log(0, want);
        _hasMore = list.Count >= want;

        int common = 0;
        while (common < _hashes.Count && common < list.Count && _hashes[common] == list[common].Hash) common++;

        string? keepHash = SelectedCommitRow?.Commit?.Hash;
        bool wasWip = SelectedCommitRow?.IsWip ?? true;

        IEnumerable<Commit> toAdd;
        if (common == _hashes.Count && _hashes.Count > 0) toAdd = list.Skip(common);
        else
        {
            // History diverged (new commit, rebase, ...): rebuild everything.
            Reset(keepWip: true);
            toAdd = list;
        }
        AppendCommits(toAdd.ToList(), withGraph: true);
        RestoreSelection(wasWip, keepHash);
    }

    public async Task LoadMoreAsync()
    {
        if (_loadingMore || !_hasMore || IsSearching) return;
        _loadingMore = true;
        try
        {
            var list = await _git.Log(_hashes.Count, PageSize);
            _hasMore = list.Count >= PageSize;
            AppendCommits(list, withGraph: true);
        }
        finally { _loadingMore = false; }
    }

    private void Reset(bool keepWip)
    {
        _suppressSelection = true;
        Commits.Clear();
        if (keepWip) Commits.Add(WipRow);
        _hashes.Clear();
        _layout = new GraphLayout();
        _maxLanes = 2;
        _suppressSelection = false;
    }

    private void RestoreSelection(bool wasWip, string? keepHash)
    {
        if (SelectedCommitRow is not null && Commits.Contains(SelectedCommitRow)) return;
        var row = !wasWip && keepHash is not null ? Commits.FirstOrDefault(r => r.Commit?.Hash == keepHash) : null;
        SelectedCommitRow = row ?? (Commits.Contains(WipRow) ? WipRow : Commits.FirstOrDefault());
    }

    private void AppendCommits(IReadOnlyList<Commit> commits, bool withGraph)
    {
        if (commits.Count == 0) return;
        var rows = withGraph ? _layout.Append(commits) : null;
        for (int i = 0; i < commits.Count; i++)
        {
            var c = commits[i];
            if (rows is not null) _maxLanes = Math.Max(_maxLanes, rows[i].Width);
            _hashes.Add(c.Hash);
            Commits.Add(new CommitRowVM
            {
                Commit = c,
                Graph = rows?[i],
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
        Metrics.Update(withGraph ? _maxLanes : 1);
    }

    public void SelectHash(string hash)
    {
        var row = Commits.FirstOrDefault(r => r.Commit?.Hash == hash);
        if (row is not null) SelectedCommitRow = row;
        else if (!IsSearching) Notify?.Invoke("El commit no está cargado en la lista todavía");
    }

    /// <summary>Optional status-bar sink (set by the parent).</summary>
    public Action<string>? Notify { get; set; }

    // ---------- search ----------

    partial void OnSearchTextChanged(string value) => ScheduleSearch();
    partial void OnSearchModeIndexChanged(int value) { if (!Query.IsEmpty) ScheduleSearch(); }

    private void ScheduleSearch()
    {
        _searchCts?.Cancel();
        var cts = _searchCts = new CancellationTokenSource();
        _ = Task.Delay(350, cts.Token).ContinueWith(t =>
        {
            if (!t.IsCanceled) Avalonia.Threading.Dispatcher.UIThread.Post(() => _ = OnSearchDue(cts.Token));
        }, TaskScheduler.Default);
    }

    private async Task OnSearchDue(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return;
        if (Query.IsEmpty)
        {
            if (!IsSearching) return;
            IsSearching = false;
            SearchStatus = "";
            Reset(keepWip: true);
            _hasMore = true;
            await ReloadAsync();
            return;
        }
        IsSearching = true;
        await RunSearchAsync();
    }

    private async Task RunSearchAsync()
    {
        var query = Query;
        SearchStatus = "Buscando…";
        var list = await _git.Search(query, SearchLimit);
        if (query != Query) return; // a newer search is on its way
        Reset(keepWip: false);
        AppendCommits(list, withGraph: false);
        SearchStatus = list.Count switch
        {
            0 => "Sin resultados",
            SearchLimit => $"Primeros {SearchLimit} resultados",
            1 => "1 resultado",
            var n => $"{n} resultados",
        };
        if (Commits.Count > 0) SelectedCommitRow = Commits[0];
    }

    [RelayCommand]
    private void ClearSearch() => SearchText = "";

    [RelayCommand]
    private void FocusSearch() => FocusSearchRequested?.Invoke();

    // ---------- context menu ----------

    public IReadOnlyList<MenuAction> MenuFor(CommitRowVM row)
    {
        if (row.Commit is not { } c) return Array.Empty<MenuAction>();
        return new[]
        {
            new MenuAction("Copiar hash", () => _clipboard.SetTextAsync(c.Hash)),
            new MenuAction("Copiar mensaje", () => _clipboard.SetTextAsync(string.IsNullOrEmpty(c.Message) ? c.Subject : c.Message)),
            MenuAction.Separator,
            new MenuAction("Checkout (HEAD desacoplado)", () => _actions.Checkout(c.Hash)),
            new MenuAction("Nueva rama aquí…", () => _actions.NewBranch(c.Hash)),
            new MenuAction("Nuevo tag aquí…", () => _actions.CreateTag(c.Hash)),
            MenuAction.Separator,
            new MenuAction("Cherry-pick", () => _actions.CherryPick(c)),
            new MenuAction("Revert", () => _actions.Revert(c)),
            MenuAction.Separator,
            new MenuAction("Reset suave (soft) hasta aquí", () => _actions.ResetTo(c, "soft")),
            new MenuAction("Reset mixto (mixed) hasta aquí", () => _actions.ResetTo(c, "mixed")),
            new MenuAction("Reset duro (hard) hasta aquí…", () => _actions.ResetTo(c, "hard")),
        };
    }
}
