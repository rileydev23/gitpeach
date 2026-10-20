namespace GitPeach.Models;

public enum SearchMode { Message, Author, Hash, Code }

/// <summary>A history filter. An empty <see cref="Text"/> means "no filter".</summary>
public sealed record LogQuery(string Text, SearchMode Mode)
{
    public bool IsEmpty => string.IsNullOrWhiteSpace(Text);
}

public enum PullMode { Default, Rebase, FastForwardOnly }

public sealed record PushOptions
{
    public string? Remote { get; init; }
    public string? Branch { get; init; }
    public bool SetUpstream { get; init; }
    public bool ForceWithLease { get; init; }
    public bool Tags { get; init; }
}

/// <summary>Side of a conflicted file to keep. Git calls them --ours / --theirs.</summary>
public enum ConflictSide { Ours, Theirs }

/// <summary>One commit that touched a file, with the file's path at that commit (renames are followed).</summary>
public sealed record FileRevision(Commit Commit, string Path);

public sealed class BlameLine
{
    public required string Hash { get; init; }
    public required int LineNo { get; init; }
    public required string Text { get; init; }
    public string Author { get; init; } = "";
    public DateTimeOffset Date { get; init; }
    public string Summary { get; init; } = "";
    public bool IsUncommitted => Hash.All(c => c == '0');
    public string ShortHash => Hash.Length > 8 ? Hash[..8] : Hash;
}
