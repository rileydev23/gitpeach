using GitPeach.Git.Parsers;
using GitPeach.Models;

namespace GitPeach.Git;

public sealed class GitRepository
{
    public string Path { get; }
    public event Action<string>? CommandLogged;

    public GitRepository(string path) => Path = path;

    private async Task<GitResult> Run(IReadOnlyList<string> args, string? stdin = null,
        Action<string>? onStderr = null, CancellationToken ct = default, bool log = false)
    {
        var res = await GitRunner.RunAsync(Path, args, stdin, onStderr, ct);
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

    // ---------- static helpers (used by the repo manager) ----------

    public static async Task<string?> FindRoot(string dir, CancellationToken ct = default)
    {
        if (!Directory.Exists(dir)) return null;
        var r = await GitRunner.RunAsync(dir, new[] { "rev-parse", "--show-toplevel" }, ct: ct);
        return r.Ok ? System.IO.Path.GetFullPath(r.Stdout.Trim()) : null;
    }

    public static async Task<string?> CurrentBranchOf(string dir, CancellationToken ct = default)
    {
        var r = await GitRunner.RunAsync(dir, new[] { "rev-parse", "--abbrev-ref", "HEAD" }, ct: ct);
        return r.Ok ? r.Stdout.Trim() : null;
    }

    public static async Task<GitResult> Clone(string url, string destination, Action<string>? progress, CancellationToken ct)
    {
        var parent = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(destination)) ?? ".";
        Directory.CreateDirectory(parent);
        return await GitRunner.RunAsync(parent, new[] { "clone", "--progress", url, destination }, onStderr: progress, ct: ct);
    }

    // ---------- reading ----------

    public async Task<List<Commit>> Log(int skip, int count, bool allRefs = true, CancellationToken ct = default)
    {
        var args = new List<string> { "log", "-z", "--topo-order", $"--skip={skip}", $"-n{count}", LogParser.Format };
        if (allRefs) args.Add("--all");
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
        var r = await Run(new[] { "stash", "list", "--format=%gd%x1f%H%x1f%at%x1f%gs" }, ct: ct);
        var list = new List<StashInfo>();
        int i = 0;
        foreach (var line in r.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.TrimEnd('\r').Split('\x1f');
            if (f.Length < 4) continue;
            long.TryParse(f[2], out var ts);
            list.Add(new StashInfo(i++, f[0], f[1], DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime(), f[3]));
        }
        return list;
    }

    public async Task<CommitInfo?> CommitDetails(string hash, CancellationToken ct = default)
    {
        var r = await Run(new[] { "show", "-s", "--format=%H%x1f%P%x1f%an%x1f%ae%x1f%at%x1f%cn%x1f%ce%x1f%ct%x1f%B", hash }, ct: ct);
        if (!r.Ok) return null;
        var f = r.Stdout.Split('\x1f', 9);
        if (f.Length < 9) return null;
        long.TryParse(f[4], out var at);
        long.TryParse(f[7], out var ct2);
        return new CommitInfo
        {
            Hash = f[0],
            Parents = f[1].Length == 0 ? Array.Empty<string>() : f[1].Split(' '),
            Author = f[2], AuthorEmail = f[3],
            AuthorDate = DateTimeOffset.FromUnixTimeSeconds(at).ToLocalTime(),
            Committer = f[5], CommitterEmail = f[6],
            CommitDate = DateTimeOffset.FromUnixTimeSeconds(ct2).ToLocalTime(),
            Message = f[8].TrimEnd(),
        };
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

    public Task<GitResult> ApplyPatch(string patch, bool cached, bool reverse)
    {
        var args = new List<string> { "apply", "--recount", "--whitespace=nowarn" };
        if (cached) args.Add("--cached");
        if (reverse) args.Add("-R");
        return Run(args, stdin: patch, log: true);
    }

    public Task<GitResult> StageHunk(DiffFile f, DiffHunk h) => ApplyPatch(PatchBuilder.ForHunk(f, h), true, false);
    public Task<GitResult> UnstageHunk(DiffFile f, DiffHunk h) => ApplyPatch(PatchBuilder.ForHunk(f, h), true, true);
    public Task<GitResult> DiscardHunk(DiffFile f, DiffHunk h) => ApplyPatch(PatchBuilder.ForHunk(f, h), false, true);

    // ---------- commit ----------

    public Task<GitResult> Commit(string message, bool amend)
    {
        var args = new List<string> { "commit", "-F", "-" };
        if (amend) args.Add("--amend");
        return Run(args, stdin: message, log: true);
    }

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
    public Task<GitResult> DeleteRemoteBranch(string remote, string branch) => Run(new[] { "push", remote, "--delete", branch }, log: true);
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
    public Task<GitResult> Pull(Action<string>? progress = null, CancellationToken ct = default)
        => Run(new[] { "pull", "--progress" }, onStderr: progress, ct: ct, log: true);
    public Task<GitResult> Push(bool setUpstream, string? remote, string? branch, bool force, Action<string>? progress = null, CancellationToken ct = default)
    {
        var args = new List<string> { "push", "--progress" };
        if (force) args.Add("--force-with-lease");
        if (setUpstream) args.Add("-u");
        if (remote is not null) args.Add(remote);
        if (branch is not null) args.Add(branch);
        return Run(args, onStderr: progress, ct: ct, log: true);
    }
}
