using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using GitPeach.Controls;
using GitPeach.Models;

namespace GitPeach.ViewModels;

public sealed class RefBadge
{
    public required string Text { get; init; }
    public required IBrush Background { get; init; }
}

/// <summary>Shared by all rows so the graph column has a uniform width.</summary>
public sealed partial class GraphMetrics : ObservableObject
{
    [ObservableProperty] private double _pixelWidth = GraphCell.LaneWidth * 2;

    public void Update(int maxLanes)
    {
        var w = Math.Clamp(maxLanes, 2, 14) * GraphCell.LaneWidth + 4;
        if (Math.Abs(w - PixelWidth) > 0.5) PixelWidth = w;
    }
}

public sealed partial class CommitRowVM : ObservableObject
{
    public Commit? Commit { get; init; }
    public GraphRow? Graph { get; init; }
    public bool IsWip { get; init; }
    public required GraphMetrics Metrics { get; init; }
    [ObservableProperty] private string _subject = "";
    public string Author { get; init; } = "";
    public string DateText { get; init; } = "";
    public string ShortHash { get; init; } = "";
    public List<RefBadge> Badges { get; init; } = new();
    public bool HasBadges => Badges.Count > 0;
    public FontWeight Weight => IsWip ? FontWeight.SemiBold : FontWeight.Normal;
}

public enum DiffRowKind { FileHeader, HunkHeader, Line, Note }

public sealed class DiffRowVM
{
    public DiffRowKind Kind { get; init; }
    public string Text { get; init; } = "";
    public string OldNo { get; init; } = "";
    public string NewNo { get; init; } = "";
    public char Prefix { get; init; } = ' ';
    public IBrush Background { get; init; } = Palette.Transparent;
    public IBrush Foreground { get; init; } = Palette.ContextFg;
    public IBrush PrefixBrush => Foreground;
    public bool IsFileHeader => Kind == DiffRowKind.FileHeader;
    public bool IsHunkHeader => Kind == DiffRowKind.HunkHeader;
    public bool IsLine => Kind == DiffRowKind.Line;
    public bool IsNote => Kind == DiffRowKind.Note;

    public DiffFile? File { get; init; }
    public DiffHunk? Hunk { get; init; }

    // Hunk actions (only populated in working-tree mode)
    public bool CanStage { get; init; }
    public bool CanUnstage { get; init; }
    public bool CanDiscard { get; init; }
    public ICommand? StageCommand { get; init; }
    public ICommand? UnstageCommand { get; init; }
    public ICommand? DiscardCommand { get; init; }
}

public sealed class FileEntryVM
{
    public required FileChange Change { get; init; }
    public string Path => Change.Path;
    public string FileName => System.IO.Path.GetFileName(Change.Path);
    public string Directory
    {
        get
        {
            var d = System.IO.Path.GetDirectoryName(Change.Path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(d) ? "" : d + "/";
        }
    }
    public string StatusText => Change.Status.ToString();
    public IBrush StatusBrush => Palette.StatusBrush(Change.Status);
    public bool Staged => Change.Staged;
    public ICommand? PrimaryCommand { get; set; }   // stage or unstage
    public string PrimaryText => Change.Staged ? "−" : "+";
    public ICommand? DiscardCommand { get; set; }
    public bool CanDiscard => !Change.Staged;
}

/// <summary>File row in commit mode: click scrolls to that file in the diff.</summary>
public sealed class CommitFileVM
{
    public required string Path { get; init; }
    public required char Status { get; init; }
    public string StatusText => Status.ToString();
    public IBrush StatusBrush => Palette.StatusBrush(Status);
    public string FileName => System.IO.Path.GetFileName(Path);
    public string Directory
    {
        get
        {
            var d = System.IO.Path.GetDirectoryName(Path)?.Replace('\\', '/');
            return string.IsNullOrEmpty(d) ? "" : d + "/";
        }
    }
}

public enum SidebarKind { Header, LocalBranch, RemoteBranch, Tag, Stash }

public sealed class SidebarItem
{
    public SidebarKind Kind { get; init; }
    public string Text { get; init; } = "";
    public string Detail { get; init; } = "";
    public bool IsCurrent { get; init; }
    public double Indent { get; init; }
    public object? Payload { get; init; }
    public Avalonia.Thickness Pad => new(Indent + 8, 3, 8, 3);
    public bool IsHeader => Kind == SidebarKind.Header;
    public FontWeight Weight => IsHeader || IsCurrent ? FontWeight.SemiBold : FontWeight.Normal;
    public IBrush Foreground => IsHeader ? Palette.Dim : Palette.Text;
}
