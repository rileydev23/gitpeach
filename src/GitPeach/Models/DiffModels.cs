namespace GitPeach.Models;

public enum DiffLineKind { Context, Added, Removed, NoNewline }

public sealed class DiffLine
{
    public required DiffLineKind Kind { get; init; }
    public required string Text { get; init; }
    public int OldNo { get; init; }
    public int NewNo { get; init; }

    public char Prefix => Kind switch
    {
        DiffLineKind.Added => '+',
        DiffLineKind.Removed => '-',
        DiffLineKind.NoNewline => '\\',
        _ => ' ',
    };
}

public sealed class DiffHunk
{
    public required string Header { get; init; }
    public int OldStart { get; init; }
    public int OldCount { get; init; }
    public int NewStart { get; init; }
    public int NewCount { get; init; }
    public string Context { get; init; } = "";
    public List<DiffLine> Lines { get; } = new();
}

public sealed class DiffFile
{
    public List<string> HeaderLines { get; } = new();
    public List<DiffHunk> Hunks { get; } = new();
    public string? OldPath { get; set; }
    public string? NewPath { get; set; }
    public bool IsBinary { get; set; }
    public bool IsNew { get; set; }
    public bool IsDeleted { get; set; }
    public string DisplayPath => NewPath ?? OldPath ?? "";
}

public sealed class GraphSegment
{
    public GraphSegment(int from, int to, int color, bool top)
    {
        From = from; To = to; Color = color; Top = top;
    }
    public int From { get; }
    public int To { get; }
    public int Color { get; }
    /// <summary>True = upper half of the row (incoming), false = lower half (outgoing).</summary>
    public bool Top { get; }
}

public sealed class GraphRow
{
    public int NodeLane { get; init; }
    public int NodeColor { get; init; }
    public bool IsMerge { get; init; }
    public required IReadOnlyList<GraphSegment> Segments { get; init; }
    public int Width { get; init; }
}
