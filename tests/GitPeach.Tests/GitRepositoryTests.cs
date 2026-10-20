using GitPeach.Git;
using GitPeach.Models;

namespace GitPeach.Tests;

/// <summary>A throw-away repository on disk, isolated from the user's git configuration.</summary>
public sealed class TempRepo : IDisposable
{
    private static readonly string IsolatedConfig;

    static TempRepo()
    {
        // Children inherit these: commits get a fixed identity and no user hooks/signing apply.
        IsolatedConfig = Path.Combine(Path.GetTempPath(), "gitpeach-tests-gitconfig");
        File.WriteAllText(IsolatedConfig, "[init]\n\tdefaultBranch = main\n[core]\n\tautocrlf = false\n");
        Environment.SetEnvironmentVariable("GIT_CONFIG_GLOBAL", IsolatedConfig);
        Environment.SetEnvironmentVariable("GIT_CONFIG_NOSYSTEM", "1");
        Environment.SetEnvironmentVariable("GIT_AUTHOR_NAME", "Tester");
        Environment.SetEnvironmentVariable("GIT_AUTHOR_EMAIL", "tester@example.com");
        Environment.SetEnvironmentVariable("GIT_COMMITTER_NAME", "Tester");
        Environment.SetEnvironmentVariable("GIT_COMMITTER_EMAIL", "tester@example.com");
    }

    public static readonly IGitRunner Runner = new GitRunner();
    public string Dir { get; }
    public GitRepository Repo { get; }

    public TempRepo(bool bare = false)
    {
        Dir = Path.Combine(Path.GetTempPath(), "gitpeach-" + Guid.NewGuid().ToString("N")[..10]);
        Directory.CreateDirectory(Dir);
        Git(bare ? new[] { "init", "--bare", "-b", "main" } : new[] { "init", "-b", "main" });
        Repo = new GitRepository(Dir, Runner);
    }

    public string Git(params string[] args)
    {
        var r = Runner.RunAsync(Dir, args).GetAwaiter().GetResult();
        if (!r.Ok) throw new InvalidOperationException($"git {string.Join(' ', args)} failed: {r.Message}");
        return r.Stdout;
    }

    public void Write(string path, string content)
    {
        var full = Path.Combine(Dir, path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    public string Read(string path) => File.ReadAllText(Path.Combine(Dir, path));

    public string Commit(string path, string content, string message, string? author = null)
    {
        Write(path, content);
        Git("add", "--", path);
        var args = new List<string> { "commit", "-q", "-m", message };
        if (author is not null) args.Add($"--author={author} <{author.ToLowerInvariant()}@example.com>");
        Git(args.ToArray());
        return Git("rev-parse", "HEAD").Trim();
    }

    public void Dispose()
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(Dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal); // git object files are read-only
            Directory.Delete(Dir, true);
        }
        catch { /* best effort */ }
    }
}

public class GitRepositoryTests
{
    [Fact]
    public async Task Stage_commit_and_read_back()
    {
        using var t = new TempRepo();
        t.Write("a.txt", "hello\n");

        var status = await t.Repo.Status();
        Assert.True(Assert.Single(status.Unstaged).IsUntracked);

        Assert.True((await t.Repo.Stage(new[] { "a.txt" })).Ok);
        Assert.True((await t.Repo.Commit("first commit\n\nbody", amend: false)).Ok);

        var log = await t.Repo.Log(0, 10);
        var c = Assert.Single(log);
        Assert.Equal("first commit", c.Subject);
        Assert.Contains(c.Refs, r => r.Name == "main" && r.IsCurrent);
        Assert.Equal("first commit\n\nbody", await t.Repo.LastCommitMessage());
        Assert.Empty((await t.Repo.Status()).Unstaged);
    }

    [Fact]
    public async Task Search_by_message_author_hash_and_code()
    {
        using var t = new TempRepo();
        var h1 = t.Commit("a.txt", "alpha\n", "Add alpha feature", "Ana");
        t.Commit("b.txt", "needle_value = 1\n", "Second change", "Bruno");
        t.Commit("c.txt", "gamma\n", "Fix typo", "Ana");

        Assert.Equal(new[] { "Add alpha feature" }, (await t.Repo.Search(new LogQuery("ALPHA", SearchMode.Message), 50)).Select(c => c.Subject));
        Assert.Equal(new[] { "Fix typo", "Add alpha feature" }, (await t.Repo.Search(new LogQuery("ana", SearchMode.Author), 50)).Select(c => c.Subject));
        Assert.Equal(h1, Assert.Single(await t.Repo.Search(new LogQuery(h1[..7], SearchMode.Hash), 50)).Hash);
        Assert.Equal("Second change", Assert.Single(await t.Repo.Search(new LogQuery("needle_value", SearchMode.Code), 50)).Subject);

        Assert.Empty(await t.Repo.Search(new LogQuery("--all", SearchMode.Hash), 50)); // not a hash: never passed to git
        Assert.Empty(await t.Repo.Search(new LogQuery("nothing-matches", SearchMode.Message), 50));
    }

    [Fact]
    public async Task File_history_follows_renames()
    {
        using var t = new TempRepo();
        t.Commit("old.txt", "one\ntwo\nthree\nfour\nfive\n", "create");
        t.Git("mv", "old.txt", "new.txt");
        t.Git("commit", "-q", "-m", "rename");
        t.Commit("new.txt", "one\ntwo\nthree\nfour\nfive\nsix\n", "edit");

        var history = await t.Repo.FileHistory("new.txt");

        Assert.Equal(new[] { "edit", "rename", "create" }, history.Select(h => h.Commit.Subject));
        Assert.Equal("old.txt", history[^1].Path);

        var diff = await t.Repo.FileDiffAt(history[0].Commit.Hash, history[0].Path);
        Assert.Contains(Assert.Single(diff).Hunks.SelectMany(h => h.Lines), l => l.Kind == DiffLineKind.Added && l.Text == "six");
    }

    [Fact]
    public async Task Blame_attributes_lines_to_commits()
    {
        using var t = new TempRepo();
        var h1 = t.Commit("f.txt", "a\nb\n", "first", "Ana");
        var h2 = t.Commit("f.txt", "a\nB\nc\n", "second", "Bruno");
        t.Write("f.txt", "a\nB\nc\nlocal\n");

        var head = await t.Repo.Blame("f.txt");
        Assert.Equal(new[] { h1, h2, h2 }, head.Take(3).Select(l => l.Hash));
        Assert.Equal("Bruno", head[1].Author);
        Assert.True(head[3].IsUncommitted);

        var atFirst = await t.Repo.Blame("f.txt", h1);
        Assert.Equal(new[] { "a", "b" }, atFirst.Select(l => l.Text));
    }

    [Fact]
    public async Task Stage_a_single_hunk()
    {
        using var t = new TempRepo();
        var lines = Enumerable.Range(1, 30).Select(i => $"line {i}").ToList();
        t.Commit("f.txt", string.Join('\n', lines) + "\n", "base");
        lines[1] = "CHANGED 2";
        lines[27] = "CHANGED 28";
        t.Write("f.txt", string.Join('\n', lines) + "\n");

        var change = (await t.Repo.Status()).Unstaged.Single();
        var diff = Assert.Single(await t.Repo.WorkingDiff(change));
        Assert.Equal(2, diff.Hunks.Count);

        Assert.True((await t.Repo.StageHunk(diff, diff.Hunks[0])).Ok);

        var staged = Assert.Single(await t.Repo.WorkingDiff(change with { Staged = true }));
        var added = staged.Hunks.SelectMany(h => h.Lines).Where(l => l.Kind == DiffLineKind.Added).Select(l => l.Text);
        Assert.Equal(new[] { "CHANGED 2" }, added);
    }

    [Theory]
    [InlineData(ConflictSide.Ours, "main version\n")]
    [InlineData(ConflictSide.Theirs, "feature version\n")]
    public async Task Resolve_merge_conflict_with_one_side(ConflictSide side, string expected)
    {
        using var t = new TempRepo();
        t.Commit("f.txt", "base\n", "base");
        t.Git("checkout", "-q", "-b", "feature");
        t.Commit("f.txt", "feature version\n", "feature");
        t.Git("checkout", "-q", "main");
        t.Commit("f.txt", "main version\n", "main");

        Assert.False((await t.Repo.Merge("feature")).Ok);
        Assert.Equal(RepoOperation.Merge, await t.Repo.CurrentOperation());
        var conflict = Assert.Single((await t.Repo.Status()).Unstaged);
        Assert.True(conflict.IsConflict);

        Assert.True((await t.Repo.UseConflictSide("f.txt", side)).Ok);

        Assert.Equal(expected, t.Read("f.txt").Replace("\r\n", "\n"));
        var status = await t.Repo.Status();
        Assert.DoesNotContain(status.Unstaged, f => f.IsConflict);
        Assert.True((await t.Repo.Continue(RepoOperation.Merge)).Ok);
        Assert.Equal(RepoOperation.None, await t.Repo.CurrentOperation());
    }

    [Fact]
    public async Task Mark_resolved_after_manual_edit()
    {
        using var t = new TempRepo();
        t.Commit("f.txt", "base\n", "base");
        t.Git("checkout", "-q", "-b", "feature");
        t.Commit("f.txt", "theirs\n", "feature");
        t.Git("checkout", "-q", "main");
        t.Commit("f.txt", "ours\n", "main");
        await t.Repo.Merge("feature");

        t.Write("f.txt", "merged by hand\n");
        Assert.True((await t.Repo.MarkResolved(new[] { "f.txt" })).Ok);

        Assert.DoesNotContain((await t.Repo.Status()).Unstaged, f => f.IsConflict);
    }

    [Fact]
    public async Task Gitignore_is_appended_once()
    {
        using var t = new TempRepo();
        t.Write(".gitignore", "bin/");   // no trailing newline
        t.Write("debug.log", "x");

        Assert.True((await t.Repo.AddToGitignore("/debug.log")).Ok);
        Assert.True((await t.Repo.AddToGitignore("/debug.log")).Ok);

        var lines = t.Read(".gitignore").Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.Length > 0);
        Assert.Equal(new[] { "bin/", "/debug.log" }, lines);
        Assert.DoesNotContain((await t.Repo.Status()).Unstaged, f => f.Path == "debug.log");
    }

    [Fact]
    public async Task Push_with_upstream_tags_and_force_with_lease()
    {
        using var remote = new TempRepo(bare: true);
        using var t = new TempRepo();
        t.Git("remote", "add", "origin", remote.Dir);
        t.Commit("a.txt", "1\n", "one");

        var first = await t.Repo.Push(new PushOptions { Remote = "origin", Branch = "main", SetUpstream = true });
        Assert.True(first.Ok, first.Message);
        Assert.Equal("origin/main", (await t.Repo.Status()).Upstream);

        Assert.True((await t.Repo.CreateTag("v1", null)).Ok);
        Assert.True((await t.Repo.PushTag("origin", "v1")).Ok);
        Assert.Contains("refs/tags/v1", remote.Git("for-each-ref", "refs/tags"));

        // Rewrite history locally, then force-push it.
        t.Commit("a.txt", "2\n", "two");
        Assert.True((await t.Repo.Push(new PushOptions())).Ok);
        t.Git("reset", "-q", "--hard", "HEAD~1");
        t.Commit("a.txt", "rewritten\n", "rewritten");
        Assert.False((await t.Repo.Push(new PushOptions())).Ok);
        Assert.True((await t.Repo.Push(new PushOptions { ForceWithLease = true })).Ok);
        Assert.Equal(t.Git("rev-parse", "HEAD"), remote.Git("rev-parse", "main"));

        Assert.True((await t.Repo.DeleteRemoteTag("origin", "v1")).Ok);
        Assert.DoesNotContain("refs/tags/v1", remote.Git("for-each-ref", "refs/tags"));
    }

    [Fact]
    public async Task Pull_fast_forward_only_and_rebase()
    {
        using var remote = new TempRepo(bare: true);
        using var a = new TempRepo();
        a.Git("remote", "add", "origin", remote.Dir);
        a.Commit("f.txt", "1\n", "one");
        a.Git("push", "-q", "-u", "origin", "main");

        using var b = new TempRepo();
        b.Git("remote", "add", "origin", remote.Dir);
        b.Git("fetch", "-q", "origin");
        b.Git("checkout", "-q", "-B", "main", "--track", "origin/main");

        a.Commit("f.txt", "2\n", "two");
        a.Git("push", "-q");
        b.Commit("other.txt", "local\n", "local work");

        Assert.False((await b.Repo.Pull(PullMode.FastForwardOnly)).Ok); // diverged
        Assert.True((await b.Repo.Pull(PullMode.Rebase)).Ok);
        var subjects = (await b.Repo.Log(0, 10)).Select(c => c.Subject);
        Assert.Equal(new[] { "local work", "two", "one" }, subjects); // linear, local commit on top
    }
}
