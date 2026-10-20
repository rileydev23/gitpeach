using GitPeach.Git;
using GitPeach.Models;

namespace GitPeach.ViewModels;

/// <summary>Latest state read from the repository; replaced as a whole on every refresh.</summary>
public sealed class RepoSnapshot
{
    public StatusInfo Status { get; init; } = new();
    public RefsSnapshot Refs { get; init; } = new();
    public RepoOperation Operation { get; init; } = RepoOperation.None;
    public IReadOnlyList<StashInfo> Stashes { get; init; } = Array.Empty<StashInfo>();
}

/// <summary>
/// What the child view models of a repository tab need from their parent: the current state and a
/// uniform way to run git commands (busy indicator, error dialog, refresh afterwards).
/// </summary>
public interface IRepoSession
{
    string RepoPath { get; }
    RepoSnapshot Snapshot { get; }

    /// <summary>Runs a command with the busy indicator, shows errors, then refreshes everything.</summary>
    Task Perform(string text, Func<Task<GitResult>> action);

    /// <summary>Runs a quick working-tree command (stage, discard...): errors are shown, only status is refreshed.</summary>
    Task PerformQuiet(Func<Task<GitResult>> action);

    /// <summary>Runs a network command, streaming git's progress to the status bar.</summary>
    Task RunRemote(string text, Func<Action<string>, Task<GitResult>> action);

    Task RefreshAsync(bool reloadCommits);
    void Notify(string text);
}
