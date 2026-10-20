namespace GitPeach.Models;

public enum RefKind { Head, LocalBranch, RemoteBranch, Tag, Other }

public sealed record RefLabel(string Name, RefKind Kind, bool IsCurrent = false);

public sealed class Commit
{
    public required string Hash { get; init; }
    public required string[] Parents { get; init; }
    public required string Author { get; init; }
    public required string Email { get; init; }
    public required DateTimeOffset Date { get; init; }
    public required string Subject { get; init; }
    public required IReadOnlyList<RefLabel> Refs { get; init; }
    public string Committer { get; init; } = "";
    public string CommitterEmail { get; init; } = "";
    public DateTimeOffset CommitDate { get; init; }
    /// <summary>Full message (subject + body), so the detail panel needs no extra git call.</summary>
    public string Message { get; init; } = "";
    public string ShortHash => Hash.Length > 8 ? Hash[..8] : Hash;
}

public sealed class CommitInfo
{
    public required string Hash { get; init; }
    public required string[] Parents { get; init; }
    public required string Author { get; init; }
    public required string AuthorEmail { get; init; }
    public required DateTimeOffset AuthorDate { get; init; }
    public required string Committer { get; init; }
    public required string CommitterEmail { get; init; }
    public required DateTimeOffset CommitDate { get; init; }
    public required string Message { get; init; }
}

public sealed record FileChange(string Path, string? OldPath, char Status, bool Staged)
{
    public bool IsUntracked => Status == '?';
    public bool IsConflict => Status == 'U';
}

public sealed class StatusInfo
{
    public string? Branch { get; set; }
    public string? Oid { get; set; }
    public string? Upstream { get; set; }
    public int Ahead { get; set; }
    public int Behind { get; set; }
    public bool IsDetached => Branch is null;
    public List<FileChange> Staged { get; } = new();
    public List<FileChange> Unstaged { get; } = new();
}

public sealed class BranchInfo
{
    public required string Name { get; init; }
    public required string FullName { get; init; }
    public required string Hash { get; init; }
    public string? Upstream { get; init; }
    public string? Track { get; init; }
    public bool IsCurrent { get; init; }
    public bool IsRemote { get; init; }
    public string? Remote { get; init; }
    /// <summary>Remote branch name without the remote prefix (e.g. "main" for "origin/main").</summary>
    public string ShortRemoteName => Remote is null ? Name : Name[(Remote.Length + 1)..];
}

public sealed record TagInfo(string Name, string Hash);

public sealed record StashInfo(int Index, string Name, string Hash, DateTimeOffset Date, string Message);

public sealed class RefsSnapshot
{
    public List<BranchInfo> Locals { get; } = new();
    public List<BranchInfo> Remotes { get; } = new();
    public List<TagInfo> Tags { get; } = new();
    public List<string> RemoteNames { get; } = new();
    public string Signature { get; set; } = "";
}

public enum RepoOperation { None, Merge, Rebase, CherryPick, Revert }
