using GitPeach.Git.Parsers;
using GitPeach.Models;

namespace GitPeach.Tests;

public class StatusParserTests
{
    [Fact]
    public void Parses_branch_headers_and_all_entry_kinds()
    {
        var output = string.Join('\0',
            "# branch.oid 1234567890abcdef1234567890abcdef12345678",
            "# branch.head main",
            "# branch.upstream origin/main",
            "# branch.ab +2 -3",
            "1 M. N... 100644 100644 100644 aaaa bbbb src/staged.cs",
            "1 .M N... 100644 100644 100644 aaaa bbbb src/unstaged.cs",
            "1 MM N... 100644 100644 100644 aaaa bbbb both.cs",
            "2 R. N... 100644 100644 100644 aaaa bbbb R100 new name.cs",
            "old name.cs",
            "u UU N... 100644 100644 100644 100644 aaaa bbbb cccc conflict.cs",
            "? untracked file.txt",
            "");

        var s = StatusParser.Parse(output);

        Assert.Equal("main", s.Branch);
        Assert.Equal("origin/main", s.Upstream);
        Assert.Equal(2, s.Ahead);
        Assert.Equal(3, s.Behind);
        Assert.False(s.IsDetached);

        Assert.Equal(new[] { "src/staged.cs", "both.cs", "new name.cs" }, s.Staged.Select(f => f.Path));
        Assert.Equal("old name.cs", s.Staged.Single(f => f.Status == 'R').OldPath);
        Assert.Equal(new[] { "src/unstaged.cs", "both.cs", "conflict.cs", "untracked file.txt" }, s.Unstaged.Select(f => f.Path));
        Assert.True(s.Unstaged.Single(f => f.Path == "conflict.cs").IsConflict);
        Assert.True(s.Unstaged.Single(f => f.Path == "untracked file.txt").IsUntracked);
    }

    [Fact]
    public void Detached_head_and_initial_commit()
    {
        var s = StatusParser.Parse("# branch.oid (initial)\0# branch.head (detached)\0");
        Assert.True(s.IsDetached);
        Assert.Null(s.Oid);
    }
}

public class LogParserTests
{
    [Fact]
    public void Parses_decorations()
    {
        var refs = LogParser.ParseRefs("HEAD -> main, origin/main, origin/HEAD, tag: v1.0, feature");
        Assert.Collection(refs,
            r => { Assert.Equal("main", r.Name); Assert.Equal(RefKind.LocalBranch, r.Kind); Assert.True(r.IsCurrent); },
            r => { Assert.Equal("origin/main", r.Name); Assert.Equal(RefKind.RemoteBranch, r.Kind); },
            r => { Assert.Equal("v1.0", r.Name); Assert.Equal(RefKind.Tag, r.Kind); },
            r => { Assert.Equal("feature", r.Name); Assert.Equal(RefKind.LocalBranch, r.Kind); });
    }

    [Fact]
    public void Parses_records()
    {
        const string us = "\x1f";
        var rec = string.Join(us, "abc123", "p1 p2", "Ana", "ana@x", "1700000000", "HEAD -> main", "Subject", "Bob", "bob@x", "1700000100", "Subject\n\nBody\n");
        var list = LogParser.Parse(rec + "\0");
        var c = Assert.Single(list);
        Assert.Equal("abc123", c.Hash);
        Assert.Equal(new[] { "p1", "p2" }, c.Parents);
        Assert.Equal("Ana", c.Author);
        Assert.Equal("Bob", c.Committer);
        Assert.Equal("Subject\n\nBody", c.Message);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1700000000), c.Date);
    }
}

public class RefParserTests
{
    [Fact]
    public void Splits_locals_remotes_and_tags()
    {
        const string us = "\x1f";
        var output = string.Join('\n',
            string.Join(us, "refs/heads/main", "h1", "*", "origin/main", "[ahead 1]", ""),
            string.Join(us, "refs/heads/dev", "h2", " ", "", "", ""),
            string.Join(us, "refs/remotes/origin/HEAD", "h1", " ", "", "", ""),
            string.Join(us, "refs/remotes/origin/main", "h1", " ", "", "", ""),
            string.Join(us, "refs/tags/v1", "tagobj", " ", "", "", "h9"));

        var snap = RefParser.Parse(output, new[] { "origin" });

        Assert.Equal(new[] { "main", "dev" }, snap.Locals.Select(b => b.Name));
        Assert.True(snap.Locals[0].IsCurrent);
        Assert.Equal("origin/main", snap.Locals[0].Upstream);
        var remote = Assert.Single(snap.Remotes);
        Assert.Equal("main", remote.ShortRemoteName);
        Assert.Equal("origin", remote.Remote);
        Assert.Equal("h9", Assert.Single(snap.Tags).Hash); // annotated tag is peeled
        Assert.NotEmpty(snap.Signature);
    }
}

public class NameStatusParserTests
{
    [Fact]
    public void Handles_renames_and_plain_entries()
    {
        var list = NameStatusParser.Parse("M\0a.txt\0R087\0old.txt\0new.txt\0D\0gone.txt\0");
        Assert.Equal(3, list.Count);
        Assert.Equal(new NameStatusEntry('R', "new.txt", "old.txt"), list[1]);
        Assert.Equal('D', list[2].Status);
    }
}

public class StashParserTests
{
    [Fact]
    public void Parses_stash_list()
    {
        var list = StashParser.Parse("stash@{0}\u001fh1\u001f1700000000\u001fOn main: wip\nstash@{1}\u001fh2\u001f1700000000\u001fWIP on dev\n");
        Assert.Equal(new[] { "stash@{0}", "stash@{1}" }, list.Select(s => s.Name));
        Assert.Equal(1, list[1].Index);
        Assert.Equal("On main: wip", list[0].Message);
    }
}

public class BlameParserTests
{
    private const string H1 = "1111111111111111111111111111111111111111";
    private const string H2 = "2222222222222222222222222222222222222222";

    [Fact]
    public void Reuses_commit_info_for_repeated_hashes()
    {
        var output = string.Join('\n',
            $"{H1} 1 1 2",
            "author Ana",
            "author-time 1700000000",
            "summary First",
            "filename f.txt",
            "\tline one",
            $"{H1} 2 2",
            "\tline two",
            $"{H2} 3 3 1",
            "author Bob",
            "author-time 1700000500",
            "summary Second",
            "filename f.txt",
            "\tline three",
            "");

        var lines = BlameParser.Parse(output);

        Assert.Equal(3, lines.Count);
        Assert.Equal(new[] { 1, 2, 3 }, lines.Select(l => l.LineNo));
        Assert.Equal("Ana", lines[1].Author);
        Assert.Equal("First", lines[1].Summary);
        Assert.Equal("line two", lines[1].Text);
        Assert.Equal("Bob", lines[2].Author);
        Assert.False(lines[0].IsUncommitted);
    }

    [Fact]
    public void Detects_uncommitted_lines()
    {
        var zero = new string('0', 40);
        var lines = BlameParser.Parse($"{zero} 1 1 1\nauthor Not Committed Yet\nauthor-time 0\nsummary x\n\tnew\n");
        Assert.True(Assert.Single(lines).IsUncommitted);
    }
}

public class FileLogParserTests
{
    [Fact]
    public void Reads_path_of_each_revision()
    {
        var output =
            "\x1e" + "h2\x1fh1\x1f" + "Ana\x1f" + "a@x\x1f" + "1700000100\x1f" + "Rename\n\nsrc/new.cs\n" +
            "\x1e" + "h1\x1f\x1f" + "Ana\x1f" + "a@x\x1f" + "1700000000\x1f" + "Create\n\nold.cs\n";

        var list = FileLogParser.Parse(output, "src/new.cs");

        Assert.Equal(new[] { "h2", "h1" }, list.Select(r => r.Commit.Hash));
        Assert.Equal(new[] { "src/new.cs", "old.cs" }, list.Select(r => r.Path));
        Assert.Empty(list[1].Commit.Parents);
    }
}
