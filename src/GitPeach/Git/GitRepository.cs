using System.Text.RegularExpressions;
using GitPeach.Git.Parsers;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.Git;

/// <summary>Facade over the git CLI for one repository. Builds arguments and parses the output.</summary>
public sealed partial class GitRepository : IGitRepository
{
    private readonly IGitRunner _runner;

    public string Path { get; }
    public event Action<string>? CommandLogged;

    public GitRepository(string path, IGitRunner runner)
    {
        Path = path;
        _runner = runner;
    }

    [GeneratedRegex("^[0-9a-fA-F]{4,40}$")]
    private static partial Regex HashRegex();

    private async Task<GitResult> Run(IReadOnlyList<string> args, string? stdin = null,
        Action<string>? onStderr = null, CancellationToken ct = default, bool log = false)
    {
        var res = await _runner.RunAsync(Path, args, stdin, onStderr, ct);
        if (log)
        {
            var text = "$ git " + string.Join(' ', args.Select(Quote));
            if (res.Stdout.Length > 0 && res.Stdout.Length < 4000) text += "\n" + res.Stdout.TrimEnd();
            if (res.Stderr.Length > 0) text += "\n" + res.Stderr.TrimEnd();
            if (!res.Ok) text += $"\n(exit {res.ExitCode})";
            CommandLogged?.Invoke(text);
        }
        return res;
    }

    private static string Quote(string s) => s.Contains(' ') ? "\"" + s + "\"" : s;

    // ---------- reading ----------

    public async Task<List<Commit>> Log(int skip, int count, CancellationToken ct = default)
    {
        var r = await Run(new[] { "log", "-z", "--topo-order", "--all", $"--skip={skip}", $"-n{count}", LogParser.Format }, ct: ct);
        return r.Ok ? LogParser.Parse(r.Stdout) : new List<Commit>();
    }

    public async Task<List<Commit>> Search(LogQuery query, int count, CancellationToken ct = default)
    {
        if (query.IsEmpty) return await Log(0, count, ct);
        var text = query.Text.Trim();
        var args = new List<string> { "log", "-z", "--topo-order", $"-n{count}", LogParser.Format };
        switch (query.Mode)
        {
            case SearchMode.Hash:
                if (!HashRegex().IsMatch(text)) return new List<Commit>();
                var rev = await Run(new[] { "rev-parse", "--verify", "--quiet", text + "^{commit}" }, ct: ct);
                if (!rev.Ok) return new List<Commit>();
                args.Add("--no-walk");
                args.Add(rev.Stdout.Trim());
                break;
            case SearchMode.Author:
                args.AddRange(new[] { "--all", "-i", "--author=" + text });
                break;
            case SearchMode.Code:
                args.AddRange(new[] { "--all", "-S" + text });
                break;
            default:
                args.AddRange(new[] { "--all", "-i", "--grep=" + text });
                break;
        }
        var r = await Run(args, ct: ct);
        return r.Ok ? LogParser.Parse(r.Stdout) : new List<Commit>();
    }

    public async Task<StatusInfo> Status(CancellationToken ct = default)
    {
        var r = await Run(new[] { "status", "--porcelain=v2", "-z", "--branch", "--untracked-files=all" }, ct: ct);
        return StatusParser.Parse(r.Stdout);
    }

    public async Task<RefsSnapshot> Refs(CancellationToken ct = default)
    {
        var remotes = await Run(new[] { "remote" }, ct: ct);
        var names = remotes.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim());
        var r = await Run(new[] { "for-each-ref", RefParser.Format, "refs/heads", "refs/remotes", "refs/tags" }, ct: ct);
        return RefParser.Parse(r.Stdout, names);
    }

    public async Task<List<StashInfo>> Stashes(CancellationToken ct = default)
    {
        var r = await Run(new[] { "stash", "list", StashParser.Format }, ct: ct);
        return StashParser.Parse(r.Stdout);
    }

    /// <summary>Changed files of a commit (fast: no patch). Merges are diffed against the first parent.</summary>
    public async Task<List<NameStatusEntry>> CommitFiles(string hash, CancellationToken ct = default)
    {
        var r = await Run(new[] { "show", "--format=", "--first-parent", "-m", "--no-ext-diff", "-M", "--name-status", "-z", hash }, ct: ct);
        return r.Ok ? NameStatusParser.Parse(r.Stdout) : new List<NameStatusEntry>();
    }

    public async Task<List<DiffFile>> CommitDiff(string hash, CancellationToken ct = default)
    {
        var r = await Run(new[] { "show", "--format=", "--first-parent", "-m", "--no-ext-diff", "-M", "--patch", hash }, ct: ct);
        return DiffParser.Parse(r.Stdout);
    }

    public async Task<List<DiffFile>> FileDiffAt(string hash, string path, CancellationToken ct = default)
    {
        var r = await Run(new[] { "show", "--format=", "--first-parent", "-m", "--no-ext-diff", "-M", "--patch", hash, "--", path }, ct: ct);
        return DiffParser.Parse(r.Stdout);
    }

    public async Task<List<DiffFile>> WorkingDiff(FileChange change, CancellationToken ct = default)
    {
        if (change.IsUntracked)
        {
            var full = System.IO.Path.Combine(Path, change.Path);
            if (Directory.Exists(full)) return new List<DiffFile>();
            var u = await Run(new[] { "diff", "--no-index", "--no-ext-diff", "--", "NUL", change.Path }, ct: ct);
            return DiffParser.Parse(u.Stdout);
        }
        var args = new List<string> { "diff", "--no-ext-diff", "-M" };
        if (change.Staged) args.Add("--cached");
        args.Add("--");
        args.Add(change.Path);
        var r = await Run(args, ct: ct);
        return DiffParser.Parse(r.Stdout);
    }

    public async Task<List<FileRevision>> FileHistory(string path, int max = 2000, CancellationToken ct = default)
    {
        var r = await Run(new[] { "log", "--follow", "-M", "--name-only", $"-n{max}", FileLogParser.Format, "--", path }, ct: ct);
        return r.Ok ? FileLogParser.Parse(r.Stdout, path) : new List<FileRevision>();
    }

    public async Task<List<BlameLine>> Blame(string path, string? revision = null, CancellationToken ct = default)
    {
        var args = new List<string> { "blame", "--porcelain" };
        if (!string.IsNullOrEmpty(revision)) args.Add(revision);
        args.Add("--");
        args.Add(path);
        var r = await Run(args, ct: ct);
        return r.Ok ? BlameParser.Parse(r.Stdout) : new List<BlameLine>();
    }

    public async Task<bool> HasHead(CancellationToken ct = default)
        => (await Run(new[] { "rev-parse", "--verify", "-q", "HEAD" }, ct: ct)).Ok;

    public async Task<RepoOperation> CurrentOperation(CancellationToken ct = default)
    {
        var r = await Run(new[] { "rev-parse", "--git-dir" }, ct: ct);
        if (!r.Ok) return RepoOperation.None;
        var gitDir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Path, r.Stdout.Trim()));
        if (Directory.Exists(System.IO.Path.Combine(gitDir, "rebase-merge")) ||
            Directory.Exists(System.IO.Path.Combine(gitDir, "rebase-apply"))) return RepoOperation.Rebase;
        if (File.Exists(System.IO.Path.Combine(gitDir, "MERGE_HEAD"))) return RepoOperation.Merge;
        if (File.Exists(System.IO.Path.Combine(gitDir, "CHERRY_PICK_HEAD"))) return RepoOperation.CherryPick;
        if (File.Exists(System.IO.Path.Combine(gitDir, "REVERT_HEAD"))) return RepoOperation.Revert;
        return RepoOperation.None;
    }

    public async Task<string> LastCommitMessage(CancellationToken ct = default)
    {
        var r = await Run(new[] { "log", "-1", "--format=%B" }, ct: ct);
        return r.Ok ? r.Stdout.TrimEnd() : "";
    }

    // ---------- staging ----------

    public Task<GitResult> Stage(IEnumerable<string> paths)
        => Run(new[] { "add", "-A", "--" }.Concat(paths).ToList(), log: true);

    public async Task<GitResult> Unstage(IEnumerable<string> paths)
    {
        var list = paths.ToList();
        if (!await HasHead())
            return await Run(new[] { "rm", "--cached", "-r", "-q", "--" }.Concat(list).ToList(), log: true);
        return await Run(new[] { "reset", "-q", "HEAD", "--" }.Concat(list).ToList(), log: true);
    }

    public async Task<GitResult> Discard(FileChange change)
    {
        if (change.IsUntracked)
            return await Run(new[] { "clean", "-f", "-d", "--", change.Path }, log: true);
        return await Run(new[] { "checkout", "--", change.Path }, log: true);
    }

    private Task<GitResult> ApplyPatch(string patch, bool cached, bool reverse)
    {
        var args = new List<string> { "apply", "--recount", "--whitespace=nowarn" };
        if (cached) args.Add("--cached");
        if (reverse) args.Add("-R");
        return Run(args, stdin: patch, log: true);
    }

    public Task<GitResult> StageHunk(DiffFile f, DiffHunk h) => ApplyPatch(PatchBuilder.ForHunk(f, h), true, false);
    public Task<GitResult> UnstageHunk(DiffFile f, DiffHunk h) => ApplyPatch(PatchBuilder.ForHunk(f, h), true, true);
    public Task<GitResult> DiscardHunk(DiffFile f, DiffHunk h) => ApplyPatch(PatchBuilder.ForHunk(f, h), false, true);

    public Task<GitResult> Commit(string message, bool amend)
    {
        var args = new List<string> { "commit", "-F", "-" };
        if (amend) args.Add("--amend");
        return Run(args, stdin: message, log: true);
    }

    public async Task<GitResult> AddToGitignore(string pattern)
    {
        var file = System.IO.Path.Combine(Path, ".gitignore");
        try
        {
            var existing = File.Exists(file) ? await File.ReadAllTextAsync(file) : "";
            var lines = existing.Split('\n').Select(l => l.TrimEnd('\r'));
            if (lines.Contains(pattern))
                return GitResult.Success();
            var prefix = existing.Length > 0 && !existing.EndsWith('\n') ? Environment.NewLine : "";
            await File.AppendAllTextAsync(file, prefix + pattern + Environment.NewLine);
            CommandLogged?.Invoke($"# .gitignore += {pattern}");
            return GitResult.Success();
        }
        catch (Exception ex)
        {
            return GitResult.Fail(Loc.F("git.gitignoreError", ex.Message));
        }
    }

    // ---------- conflicts ----------

    public async Task<GitResult> UseConflictSide(string path, ConflictSide side)
    {
        var r = await Run(new[] { "checkout", side == ConflictSide.Ours ? "--ours" : "--theirs", "--", path }, log: true);
        return r.Ok ? await Run(new[] { "add", "--", path }, log: true) : r;
    }

    public Task<GitResult> MarkResolved(IEnumerable<string> paths)
        => Run(new[] { "add", "--" }.Concat(paths).ToList(), log: true);

    public Task<GitResult> RemoveConflicted(string path) => Run(new[] { "rm", "-q", "--", path }, log: true);

    public async Task<bool> HasMergeTool()
    {
        var r = await Run(new[] { "config", "--get", "merge.tool" });
        return r.Ok && r.Stdout.Trim().Length > 0;
    }

    public Task<GitResult> MergeTool(string path) => Run(new[] { "mergetool", "--no-prompt", "--", path }, log: true);

    // ---------- branches / refs ----------

    public Task<GitResult> Checkout(string name) => Run(new[] { "checkout", name }, log: true);
    public Task<GitResult> CheckoutRemote(string remoteBranch, string localName)
        => Run(new[] { "checkout", "-b", localName, "--track", remoteBranch }, log: true);
    public Task<GitResult> CreateBranch(string name, string? startPoint, bool checkout)
    {
        var args = checkout ? new List<string> { "checkout", "-b", name } : new List<string> { "branch", name };
        if (!string.IsNullOrWhiteSpace(startPoint)) args.Add(startPoint);
        return Run(args, log: true);
    }
    public Task<GitResult> RenameBranch(string oldName, string newName) => Run(new[] { "branch", "-m", oldName, newName }, log: true);
    public Task<GitResult> DeleteBranch(string name, bool force) => Run(new[] { "branch", force ? "-D" : "-d", name }, log: true);
    public Task<GitResult> CreateTag(string name, string? target) => Run(target is null ? new[] { "tag", name } : new[] { "tag", name, target }, log: true);
    public Task<GitResult> DeleteTag(string name) => Run(new[] { "tag", "-d", name }, log: true);
    public Task<GitResult> Merge(string name) => Run(new[] { "merge", name }, log: true);
    public Task<GitResult> Rebase(string onto) => Run(new[] { "rebase", onto }, log: true);
    public Task<GitResult> CherryPick(string hash) => Run(new[] { "cherry-pick", hash }, log: true);
    public Task<GitResult> Revert(string hash) => Run(new[] { "revert", "--no-edit", hash }, log: true);
    public Task<GitResult> ResetTo(string hash, string mode) => Run(new[] { "reset", "--" + mode, hash }, log: true);
    public Task<GitResult> Abort(RepoOperation op) => Run(new[] { OpName(op), "--abort" }, log: true);
    public Task<GitResult> Continue(RepoOperation op) => Run(new[] { OpName(op), "--continue" }, log: true);

    private static string OpName(RepoOperation op) => op switch
    {
        RepoOperation.Merge => "merge",
        RepoOperation.Rebase => "rebase",
        RepoOperation.CherryPick => "cherry-pick",
        RepoOperation.Revert => "revert",
        _ => "status",
    };

    // ---------- stash ----------

    public Task<GitResult> StashPush(string? message)
        => Run(string.IsNullOrWhiteSpace(message)
            ? new[] { "stash", "push", "--include-untracked" }
            : new[] { "stash", "push", "--include-untracked", "-m", message }, log: true);
    public Task<GitResult> StashPop(string name) => Run(new[] { "stash", "pop", name }, log: true);
    public Task<GitResult> StashApply(string name) => Run(new[] { "stash", "apply", name }, log: true);
    public Task<GitResult> StashDrop(string name) => Run(new[] { "stash", "drop", name }, log: true);

    // ---------- remotes ----------

    public Task<GitResult> Fetch(Action<string>? progress = null, CancellationToken ct = default)
        => Run(new[] { "fetch", "--all", "--prune", "--progress" }, onStderr: progress, ct: ct, log: true);

    public Task<GitResult> Pull(PullMode mode, Action<string>? progress = null, CancellationToken ct = default)
    {
        var args = new List<string> { "pull", "--progress" };
        if (mode == PullMode.Rebase) args.Add("--rebase");
        else if (mode == PullMode.FastForwardOnly) args.Add("--ff-only");
        return Run(args, onStderr: progress, ct: ct, log: true);
    }

    public Task<GitResult> Push(PushOptions o, Action<string>? progress = null, CancellationToken ct = default)
    {
        var args = new List<string> { "push", "--progress" };
        if (o.ForceWithLease) args.Add("--force-with-lease");
        if (o.SetUpstream) args.Add("-u");
        if (o.Tags) args.Add("--tags");
        if (o.Remote is not null) args.Add(o.Remote);
        if (o.Branch is not null) args.Add(o.Branch);
        return Run(args, onStderr: progress, ct: ct, log: true);
    }

    public Task<GitResult> PushTag(string remote, string tag, Action<string>? progress = null, CancellationToken ct = default)
        => Run(new[] { "push", "--progress", remote, "refs/tags/" + tag }, onStderr: progress, ct: ct, log: true);

    public Task<GitResult> DeleteRemoteBranch(string remote, string branch) => Run(new[] { "push", remote, "--delete", branch }, log: true);
    public Task<GitResult> DeleteRemoteTag(string remote, string tag) => Run(new[] { "push", remote, "--delete", "refs/tags/" + tag }, log: true);
}

public sealed class GitRepositoryFactory(IGitRunner runner) : IGitRepositoryFactory
{
    public IGitRepository Open(string path) => new GitRepository(path, runner);
}

public sealed class GitHost(IGitRunner runner) : IGitHost
{
    public async Task<string?> FindRoot(string dir, CancellationToken ct = default)
    {
        if (!Directory.Exists(dir)) return null;
        var r = await runner.RunAsync(dir, new[] { "rev-parse", "--show-toplevel" }, ct: ct);
        return r.Ok ? Path.GetFullPath(r.Stdout.Trim()) : null;
    }

    public async Task<string?> CurrentBranchOf(string dir, CancellationToken ct = default)
    {
        var r = await runner.RunAsync(dir, new[] { "rev-parse", "--abbrev-ref", "HEAD" }, ct: ct);
        return r.Ok ? r.Stdout.Trim() : null;
    }

    public async Task<GitResult> Clone(string url, string destination, Action<string>? progress, CancellationToken ct)
    {
        var parent = Path.GetDirectoryName(Path.GetFullPath(destination)) ?? ".";
        Directory.CreateDirectory(parent);
        return await runner.RunAsync(parent, new[] { "clone", "--progress", url, destination }, onStderr: progress, ct: ct);
    }
}
