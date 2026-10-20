using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Git;
using GitPeach.Models;

namespace GitPeach.ViewModels;

/// <summary>Unstaged / staged file lists, conflict summary and the commit box.</summary>
public sealed partial class WorkingTreeViewModel : ObservableObject
{
    private readonly IRepoSession _session;
    private readonly IGitQueries _queries;
    private readonly IGitWorkingTree _git;
    private bool _suppressSelection;

    public FileActions Files { get; }
    public ObservableCollection<FileEntryVM> Staged { get; } = new();
    public ObservableCollection<FileEntryVM> Unstaged { get; } = new();

    [ObservableProperty] private FileEntryVM? _selectedUnstaged;
    [ObservableProperty] private FileEntryVM? _selectedStaged;
    [ObservableProperty] private string _commitMessage = "";
    [ObservableProperty] private bool _amend;
    [ObservableProperty] private int _conflictCount;

    public bool HasConflicts => ConflictCount > 0;
    public string ConflictText => ConflictCount == 1
        ? "1 archivo en conflicto — clic derecho sobre él para resolverlo"
        : $"{ConflictCount} archivos en conflicto — clic derecho sobre cada uno para resolverlos";

    /// <summary>The file whose diff should be shown.</summary>
    public FileChange? SelectedChange => (SelectedUnstaged ?? SelectedStaged)?.Change;

    public event Action? SelectionChanged;
    public event Action? CommitMessageFocusRequested;

    public WorkingTreeViewModel(IRepoSession session, IGitQueries queries, IGitWorkingTree git, FileActions files)
    {
        _session = session;
        _queries = queries;
        _git = git;
        Files = files;
    }

    partial void OnConflictCountChanged(int value)
    {
        OnPropertyChanged(nameof(HasConflicts));
        OnPropertyChanged(nameof(ConflictText));
    }

    public void Apply(StatusInfo status)
    {
        ConflictCount = status.Unstaged.Count(f => f.IsConflict);

        var selUn = SelectedUnstaged?.Path;
        var selSt = SelectedStaged?.Path;
        _suppressSelection = true;
        Sync(Unstaged, status.Unstaged);
        Sync(Staged, status.Staged);
        SelectedUnstaged = selUn is null ? null : Unstaged.FirstOrDefault(f => f.Path == selUn);
        SelectedStaged = selSt is null ? null : Staged.FirstOrDefault(f => f.Path == selSt);
        _suppressSelection = false;
    }

    private void Sync(ObservableCollection<FileEntryVM> target, List<FileChange> source)
    {
        // Rebuild only when the list actually differs (avoids flicker and selection loss).
        var sorted = source.OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase).ToList();
        if (target.Count == sorted.Count && target.Select(t => t.Change).SequenceEqual(sorted)) return;

        target.Clear();
        foreach (var change in sorted)
        {
            target.Add(new FileEntryVM
            {
                Change = change,
                PrimaryCommand = new AsyncRelayCommand(() => change.Staged ? Files.Unstage(new[] { change.Path }) : Files.Stage(new[] { change.Path })),
                DiscardCommand = new AsyncRelayCommand(() => Files.Discard(change)),
            });
        }
    }

    partial void OnSelectedUnstagedChanged(FileEntryVM? value) => OnSelection(value, isStaged: false);
    partial void OnSelectedStagedChanged(FileEntryVM? value) => OnSelection(value, isStaged: true);

    private void OnSelection(FileEntryVM? value, bool isStaged)
    {
        if (_suppressSelection) return;
        if (value is not null)
        {
            // Only one list has a selection at a time.
            _suppressSelection = true;
            if (isStaged) SelectedUnstaged = null; else SelectedStaged = null;
            _suppressSelection = false;
        }
        SelectionChanged?.Invoke();
    }

    public IReadOnlyList<MenuAction> MenuFor(FileEntryVM entry) => Files.MenuFor(entry);

    [RelayCommand]
    private Task StageAll() => Unstaged.Count == 0 ? Task.CompletedTask : Files.Stage(new[] { "." });

    [RelayCommand]
    private Task UnstageAll() => Staged.Count == 0 ? Task.CompletedTask : Files.Unstage(new[] { "." });

    partial void OnAmendChanged(bool value)
    {
        if (value && string.IsNullOrWhiteSpace(CommitMessage))
            _ = LoadAmendMessageAsync();
    }

    private async Task LoadAmendMessageAsync() => CommitMessage = await _queries.LastCommitMessage();

    [RelayCommand]
    private async Task CommitChanges()
    {
        if (string.IsNullOrWhiteSpace(CommitMessage))
        {
            _session.Notify("Escribe un mensaje de commit");
            CommitMessageFocusRequested?.Invoke();
            return;
        }
        if (Staged.Count == 0 && !Amend) { _session.Notify("No hay cambios en stage"); return; }
        if (HasConflicts) { _session.Notify("Resuelve los conflictos antes de hacer commit"); return; }

        var msg = CommitMessage;
        var amend = Amend;
        GitResult? result = null;
        await _session.Perform("commit", async () => result = await _git.Commit(msg, amend));
        if (result is { Ok: true })
        {
            CommitMessage = "";
            Amend = false;
            _session.Notify("Commit creado");
        }
    }
}
