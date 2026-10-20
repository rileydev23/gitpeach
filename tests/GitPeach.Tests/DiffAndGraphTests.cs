using GitPeach.Git;
using GitPeach.Git.Parsers;
using GitPeach.Models;
using GitPeach.ViewModels;

namespace GitPeach.Tests;

public class DiffParserTests
{
    private const string Diff =
        "diff --git a/f.txt b/f.txt\n" +
        "index 111..222 100644\n" +
        "--- a/f.txt\n" +
        "+++ b/f.txt\n" +
        "@@ -1,3 +1,3 @@ header ctx\n" +
        " one\n" +
        "-two\n" +
        "+TWO\n" +
        " three\n" +
        "@@ -10 +10,2 @@\n" +
        " ten\n" +
        "+eleven\n" +
        "\\ No newline at end of file\n" +
        "diff --git a/new.bin b/new.bin\n" +
        "new file mode 100644\n" +
        "Binary files /dev/null and b/new.bin differ\n";

    [Fact]
    public void Parses_files_hunks_and_line_numbers()
    {
        var files = DiffParser.Parse(Diff);

        Assert.Equal(2, files.Count);
        var f = files[0];
        Assert.Equal("f.txt", f.DisplayPath);
        Assert.Equal(2, f.Hunks.Count);
        Assert.Equal("header ctx", f.Hunks[0].Context);
        Assert.Equal(1, f.Hunks[1].OldCount); // omitted count means 1

        var removed = f.Hunks[0].Lines.Single(l => l.Kind == DiffLineKind.Removed);
        var added = f.Hunks[0].Lines.Single(l => l.Kind == DiffLineKind.Added);
        Assert.Equal((2, "two"), (removed.OldNo, removed.Text));
        Assert.Equal((2, "TWO"), (added.NewNo, added.Text));
        Assert.Equal(DiffLineKind.NoNewline, f.Hunks[1].Lines[^1].Kind);

        Assert.True(files[1].IsNew);
        Assert.True(files[1].IsBinary);
    }

    [Fact]
    public void Patch_for_one_hunk_keeps_header_and_only_that_hunk()
    {
        var f = DiffParser.Parse(Diff)[0];
        var patch = PatchBuilder.ForHunk(f, f.Hunks[1]);

        Assert.StartsWith("diff --git a/f.txt b/f.txt\n", patch);
        Assert.Contains("+++ b/f.txt\n@@ -10 +10,2 @@\n ten\n+eleven\n\\ No newline at end of file\n", patch);
        Assert.DoesNotContain("TWO", patch);
    }

    [Fact]
    public void Row_builder_adds_hunk_buttons_only_in_working_tree_mode()
    {
        var files = DiffParser.Parse(Diff);

        var commitRows = DiffRowBuilder.Build(files);
        Assert.All(commitRows.Where(r => r.IsHunkHeader), r => Assert.False(r.CanStage || r.CanUnstage || r.CanDiscard));
        Assert.Contains(commitRows, r => r.IsNote && r.Text.Contains("binario"));
        Assert.Equal(2 + 2 + 4 + 3 + 1, commitRows.Count); // 2 file headers, 2 hunk headers, 7 lines, 1 note

        var stagedRows = DiffRowBuilder.Build(files, new NoopHunkActions(), staged: true);
        Assert.All(stagedRows.Where(r => r.IsHunkHeader), r => { Assert.True(r.CanUnstage); Assert.False(r.CanStage); });
    }

    private sealed class NoopHunkActions : IHunkActions
    {
        private static readonly System.Windows.Input.ICommand Cmd = new CommunityToolkit.Mvvm.Input.RelayCommand(() => { });
        public System.Windows.Input.ICommand Stage(DiffFile f, DiffHunk h) => Cmd;
        public System.Windows.Input.ICommand Unstage(DiffFile f, DiffHunk h) => Cmd;
        public System.Windows.Input.ICommand Discard(DiffFile f, DiffHunk h) => Cmd;
    }
}

public class GraphLayoutTests
{
    private static Commit C(string hash, params string[] parents) => new()
    {
        Hash = hash, Parents = parents, Author = "", Email = "", Date = default, Subject = hash, Refs = Array.Empty<RefLabel>(),
    };

    [Fact]
    public void Linear_history_stays_in_lane_zero()
    {
        var rows = new GraphLayout().Append(new[] { C("c", "b"), C("b", "a"), C("a") });
        Assert.All(rows, r => Assert.Equal(0, r.NodeLane));
        Assert.All(rows, r => Assert.False(r.IsMerge));
    }

    [Fact]
    public void Merge_opens_a_second_lane_that_closes_at_the_fork()
    {
        // m merges b2 (side branch) into b1; both come from a.
        var rows = new GraphLayout().Append(new[] { C("m", "b1", "b2"), C("b2", "a"), C("b1", "a"), C("a") });

        Assert.True(rows[0].IsMerge);
        Assert.Equal(2, rows[0].Width);
        Assert.Equal(1, rows[1].NodeLane);   // side branch commit on its own lane
        Assert.Equal(0, rows[2].NodeLane);
        // b1's parent is already awaited on lane 1, so b1 joins it and lane 0 ends there.
        Assert.Contains(rows[2].Segments, s => !s.Top && s.From == 0 && s.To == 1);
        Assert.Equal(1, rows[3].NodeLane);
        Assert.Equal(2, rows.Max(r => r.Width));
    }

    [Fact]
    public void Paging_continues_the_same_layout()
    {
        var layout = new GraphLayout();
        layout.Append(new[] { C("m", "b1", "b2") });
        var next = layout.Append(new[] { C("b2", "a") });
        Assert.Equal(1, next[0].NodeLane);
    }
}
