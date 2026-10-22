using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Git;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>Commits that touched one file (following renames) and the file's diff in each of them.</summary>
public sealed partial class FileHistoryViewModel : ObservableObject, IDisposable
{
    private readonly IGitQueries _git;
    private readonly FileActions _actions;
    private CancellationTokenSource? _cts;

    public string FilePath { get; }
    public string Title => Loc.F("fileHistory.title", FilePath);
    public DiffPaneViewModel Diff { get; } = new();

    [ObservableProperty] private IReadOnlyList<FileRevisionVM> _revisions = Array.Empty<FileRevisionVM>();
    [ObservableProperty] private FileRevisionVM? _selected;
    [ObservableProperty] private string _status = Loc.T("fileHistory.loading");
    [ObservableProperty] private string _detail = "";

    public FileHistoryViewModel(IGitQueries git, string path, FileActions actions)
    {
        _git = git;
        _actions = actions;
        FilePath = path.Replace('\\', '/');
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        var list = await _git.FileHistory(FilePath);
        Revisions = list.Select(r => new FileRevisionVM
        {
            Revision = r,
            PathNote = r.Path != FilePath ? Loc.F("fileHistory.as", r.Path) : "",
        }).ToList();
        Status = Revisions.Count switch
        {
            0 => Loc.T("fileHistory.none"),
            var n => Loc.Plural("common.commits", n),
        };
        Selected = Revisions.FirstOrDefault();
    }

    partial void OnSelectedChanged(FileRevisionVM? value) => _ = ShowAsync(value);

    private async Task ShowAsync(FileRevisionVM? rev)
    {
        _cts?.Cancel();
        var cts = _cts = new CancellationTokenSource();
        if (rev is null) { Diff.Clear(); Detail = ""; return; }
        var c = rev.Revision.Commit;
        Detail = $"{c.Hash}\n{c.Author} <{c.Email}> · {c.Date:yyyy-MM-dd HH:mm}\n\n{c.Subject}";
        Diff.Rows = DiffPaneViewModel.Loading;
        try
        {
            var files = await _git.FileDiffAt(c.Hash, rev.Revision.Path, cts.Token);
            if (cts.IsCancellationRequested) return;
            Diff.Rows = files.Count == 0
                ? new[] { DiffRowBuilder.Note(Loc.T("fileHistory.noContent")) }
                : await Task.Run(() => DiffRowBuilder.Build(files), cts.Token);
        }
        catch (OperationCanceledException) { }
    }

    [RelayCommand]
    private void BlameSelected()
    {
        if (Selected is { } r) _actions.ShowBlame(r.Revision.Path, r.Revision.Commit.Hash);
    }

    public void Dispose() => _cts?.Cancel();
}
