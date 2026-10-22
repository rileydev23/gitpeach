using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Controls;
using GitPeach.Git;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>Commands attached to the header of each hunk in working-tree diffs.</summary>
public interface IHunkActions
{
    ICommand Stage(DiffFile file, DiffHunk hunk);
    ICommand Unstage(DiffFile file, DiffHunk hunk);
    ICommand Discard(DiffFile file, DiffHunk hunk);
}

public sealed class HunkActions(IRepoSession session, IGitWorkingTree git, IDialogService dialogs) : IHunkActions
{
    public ICommand Stage(DiffFile f, DiffHunk h) => new AsyncRelayCommand(() => session.PerformQuiet(() => git.StageHunk(f, h)));
    public ICommand Unstage(DiffFile f, DiffHunk h) => new AsyncRelayCommand(() => session.PerformQuiet(() => git.UnstageHunk(f, h)));
    public ICommand Discard(DiffFile f, DiffHunk h) => new AsyncRelayCommand(async () =>
    {
        if (await dialogs.Confirm(Loc.T("diff.discardHunk.title"), Loc.T("diff.discardHunk.text"), Loc.T("common.discard")))
            await session.PerformQuiet(() => git.DiscardHunk(f, h));
    });
}

/// <summary>Turns parsed diffs into display rows. Pure (no UI access), so it runs on a background thread.</summary>
public static class DiffRowBuilder
{
    public const int MaxRows = 20000;

    /// <param name="hunkActions">Only for working-tree diffs; null for commit diffs (no hunk buttons).</param>
    public static List<DiffRowVM> Build(IReadOnlyList<DiffFile> files, IHunkActions? hunkActions = null, bool staged = false, bool untracked = false)
    {
        var rows = new List<DiffRowVM>();
        bool wip = hunkActions is not null;
        foreach (var file in files)
        {
            var title = file.DisplayPath;
            if (file.OldPath is not null && file.NewPath is not null && file.OldPath != file.NewPath)
                title = $"{file.OldPath} → {file.NewPath}";
            rows.Add(new DiffRowVM { Kind = DiffRowKind.FileHeader, Text = title, File = file, Background = Palette.FileBg, Foreground = Palette.Text });

            if (file.IsBinary)
            {
                rows.Add(Note(Loc.T("diff.binary")));
                continue;
            }
            if (file.Hunks.Count == 0)
            {
                rows.Add(Note(Loc.T(file.IsNew ? "diff.empty" : "diff.noContent")));
                continue;
            }

            foreach (var hunk in file.Hunks)
            {
                rows.Add(new DiffRowVM
                {
                    Kind = DiffRowKind.HunkHeader,
                    Text = hunk.Header,
                    File = file, Hunk = hunk,
                    Background = Palette.HunkBg, Foreground = Palette.Dim,
                    CanStage = wip && !staged && !untracked,
                    CanUnstage = wip && staged,
                    CanDiscard = wip && !staged && !untracked,
                    StageCommand = hunkActions?.Stage(file, hunk),
                    UnstageCommand = hunkActions?.Unstage(file, hunk),
                    DiscardCommand = hunkActions?.Discard(file, hunk),
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
                if (rows.Count > MaxRows)
                {
                    rows.Add(Note(Loc.F("diff.truncated", MaxRows)));
                    return rows;
                }
            }
        }
        return rows;
    }

    public static DiffRowVM Note(string text) => new() { Kind = DiffRowKind.Note, Text = text, Foreground = Palette.Dim };
}
