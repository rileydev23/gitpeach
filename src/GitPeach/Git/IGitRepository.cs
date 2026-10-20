using GitPeach.Models;
using GitPeach.Git.Parsers;

namespace GitPeach.Git;

// Interface segregation: each view model depends only on the slice of git it actually uses.
// GitRepository implements all of them; IGitRepository is the aggregate for the composition root.

/// <summary>Read-only queries.</summary>
public interface IGitQueries
{
    string Path { get; }
    Task<List<Commit>> Log(int skip, int count, CancellationToken ct = default);
    Task<List<Commit>> Search(LogQuery query, int count, CancellationToken ct = default);
    Task<StatusInfo> Status(CancellationToken ct = default);
    Task<RefsSnapshot> Refs(CancellationToken ct = default);
    Task<List<StashInfo>> Stashes(CancellationToken ct = default);
    Task<List<NameStatusEntry>> CommitFiles(string hash, CancellationToken ct = default);
    Task<List<DiffFile>> CommitDiff(string hash, CancellationToken ct = default);
    Task<List<DiffFile>> FileDiffAt(string hash, string path, CancellationToken ct = default);
    Task<List<DiffFile>> WorkingDiff(FileChange change, CancellationToken ct = default);
    Task<List<FileRevision>> FileHistory(string path, int max = 2000, CancellationToken ct = default);
    Task<List<BlameLine>> Blame(string path, string? revision = null, CancellationToken ct = default);
    Task<bool> HasHead(CancellationToken ct = default);
    Task<RepoOperation> CurrentOperation(CancellationToken ct = default);
    Task<string> LastCommitMessage(CancellationToken ct = default);
}

/// <summary>Index and working tree changes.</summary>
public interface IGitWorkingTree
{
    Task<GitResult> Stage(IEnumerable<string> paths);
    Task<GitResult> Unstage(IEnumerable<string> paths);
    Task<GitResult> Discard(FileChange change);
    Task<GitResult> StageHunk(DiffFile f, DiffHunk h);
    Task<GitResult> UnstageHunk(DiffFile f, DiffHunk h);
    Task<GitResult> DiscardHunk(DiffFile f, DiffHunk h);
    Task<GitResult> Commit(string message, bool amend);
    Task<GitResult> AddToGitignore(string pattern);
}

/// <summary>Merge-conflict resolution.</summary>
public interface IGitConflicts
{
    Task<GitResult> UseConflictSide(string path, ConflictSide side);
    Task<GitResult> MarkResolved(IEnumerable<string> paths);
    Task<GitResult> RemoveConflicted(string path);
    Task<GitResult> MergeTool(string path);
}

/// <summary>Branches, tags and history-rewriting operations.</summary>
public interface IGitBranching
{
    Task<GitResult> Checkout(string name);
    Task<GitResult> CheckoutRemote(string remoteBranch, string localName);
    Task<GitResult> CreateBranch(string name, string? startPoint, bool checkout);
    Task<GitResult> RenameBranch(string oldName, string newName);
    Task<GitResult> DeleteBranch(string name, bool force);
    Task<GitResult> CreateTag(string name, string? target);
    Task<GitResult> DeleteTag(string name);
    Task<GitResult> Merge(string name);
    Task<GitResult> Rebase(string onto);
    Task<GitResult> CherryPick(string hash);
    Task<GitResult> Revert(string hash);
    Task<GitResult> ResetTo(string hash, string mode);
    Task<GitResult> Abort(RepoOperation op);
    Task<GitResult> Continue(RepoOperation op);
}

public interface IGitStash
{
    Task<GitResult> StashPush(string? message);
    Task<GitResult> StashPop(string name);
    Task<GitResult> StashApply(string name);
    Task<GitResult> StashDrop(string name);
}

public interface IGitRemotes
{
    Task<GitResult> Fetch(Action<string>? progress = null, CancellationToken ct = default);
    Task<GitResult> Pull(PullMode mode, Action<string>? progress = null, CancellationToken ct = default);
    Task<GitResult> Push(PushOptions options, Action<string>? progress = null, CancellationToken ct = default);
    Task<GitResult> PushTag(string remote, string tag, Action<string>? progress = null, CancellationToken ct = default);
    Task<GitResult> DeleteRemoteBranch(string remote, string branch);
    Task<GitResult> DeleteRemoteTag(string remote, string tag);
}

public interface IGitRepository : IGitQueries, IGitWorkingTree, IGitConflicts, IGitBranching, IGitStash, IGitRemotes
{
    /// <summary>Raised (on any thread) with the command line and output of every mutating command.</summary>
    event Action<string>? CommandLogged;
}

public interface IGitRepositoryFactory
{
    IGitRepository Open(string path);
}

/// <summary>Git operations that are not bound to an opened repository.</summary>
public interface IGitHost
{
    Task<string?> FindRoot(string dir, CancellationToken ct = default);
    Task<string?> CurrentBranchOf(string dir, CancellationToken ct = default);
    Task<GitResult> Clone(string url, string destination, Action<string>? progress, CancellationToken ct);
}
