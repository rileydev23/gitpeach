using System.Text;
using GitPeach.Git.Parsers;
using GitPeach.Models;
using GitPeach.Services;
using GitPeach.ViewModels;

namespace GitPeach.Tests;

public class ConflictParserTests
{
    [Fact]
    public void Splits_common_text_and_blocks_keeping_line_endings()
    {
        var text = "a\r\n<<<<<<< HEAD\r\nmine\r\n=======\r\ntheirs\r\n>>>>>>> feature\r\nz\r\n";

        var s = ConflictParser.Parse(text);

        Assert.Equal(3, s.Count);
        Assert.Equal("a\r\n", Assert.IsType<CommonSegment>(s[0]).Text);
        var b = Assert.IsType<ConflictBlock>(s[1]);
        Assert.Equal("mine\r\n", b.Ours);
        Assert.Equal("theirs\r\n", b.Theirs);
        Assert.Null(b.Base);
        Assert.Equal("HEAD", b.OursLabel);
        Assert.Equal("feature", b.TheirsLabel);
        Assert.Equal("z\r\n", Assert.IsType<CommonSegment>(s[2]).Text);
    }

    [Fact]
    public void Reads_diff3_base_and_empty_sides()
    {
        var s = ConflictParser.Parse("<<<<<<< ours\n||||||| base\nold\n=======\nnew\n>>>>>>> theirs\n");

        var b = Assert.IsType<ConflictBlock>(Assert.Single(s));
        Assert.Equal("", b.Ours);
        Assert.Equal("old\n", b.Base);
        Assert.Equal("new\n", b.Theirs);
    }

    [Fact]
    public void Unterminated_or_lookalike_markers_are_plain_text()
    {
        var text = "<<<<<<< HEAD\nmine\n=======\nno end\n";
        Assert.False(ConflictParser.HasConflicts(text));
        Assert.Equal(text, Assert.IsType<CommonSegment>(Assert.Single(ConflictParser.Parse(text))).Text);
        Assert.False(ConflictParser.HasConflicts("<<<<<<<< eight\n=======\n>>>>>>> x\n"));
    }
}

public class ConflictResolutionTests
{
    private static ConflictBlockVM Block(string ours, string theirs) =>
        new(new ConflictBlock(ours, null, theirs, "HEAD", "feature"), 1, "Mía", "Entrante");

    [Fact]
    public void Whole_sides_keep_their_exact_text()
    {
        var b = Block("one\r\n", "two\r\n");
        Assert.False(b.IsResolved);

        b.Choose(ConflictChoice.Theirs);

        Assert.True(b.IsResolved);
        Assert.Equal("two", b.Result);
        Assert.Equal("two\r\n", b.ResultFor("\r\n"));
    }

    [Fact]
    public void Both_and_hand_edits_use_the_file_line_endings()
    {
        var b = Block("one\n", "two\n");
        b.Choose(ConflictChoice.OursThenTheirs);
        Assert.Equal("one\ntwo\n", b.ResultFor("\n"));

        b.Result = "custom\r\nline";
        Assert.Equal(ConflictChoice.Custom, b.Choice);
        Assert.Equal("custom\nline\n", b.ResultFor("\n"));

        b.ResetCommand.Execute(null);
        Assert.False(b.IsResolved);
    }

    [Fact]
    public void Long_common_text_collapses_to_context()
    {
        var text = string.Concat(Enumerable.Range(1, 20).Select(i => $"l{i}\n"));
        var middle = new CommonSegmentVM(new CommonSegment(text), isFirst: false, isLast: false);
        Assert.Equal("l1\nl2\nl3\n⋯ 14 líneas sin conflicto ⋯\nl18\nl19\nl20", middle.Display);

        var first = new CommonSegmentVM(new CommonSegment(text), isFirst: true, isLast: false);
        Assert.StartsWith("⋯ 17 líneas", first.Display);
    }
}

public class TextFileTests
{
    [Fact]
    public async Task Round_trips_encoding_and_detects_binary()
    {
        var path = Path.Combine(Path.GetTempPath(), "gitpeach-textfile-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            await File.WriteAllTextAsync(path, "hola ñ\n", new UTF8Encoding(true));
            var f = Assert.IsType<TextFile>(await TextFile.ReadAsync(path));
            Assert.Equal("hola ñ\n", f.Text);
            await f.WriteAsync(path, "chau\n");
            var bytes = await File.ReadAllBytesAsync(path);
            Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);

            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 0, 3 });
            Assert.Null(await TextFile.ReadAsync(path));
        }
        finally { File.Delete(path); }
    }
}

public class MergeToolConfigTests
{
    [Fact]
    public async Task Merge_tool_is_detected_from_config()
    {
        using var t = new TempRepo();
        Assert.False(await t.Repo.HasMergeTool());
        t.Git("config", "merge.tool", "meld");
        Assert.True(await t.Repo.HasMergeTool());
    }

    [Fact]
    public async Task Real_conflict_parses_into_one_block()
    {
        using var t = new TempRepo();
        t.Commit("f.txt", "top\nbase\nbottom\n", "base");
        t.Git("checkout", "-q", "-b", "feature");
        t.Commit("f.txt", "top\nfeature\nbottom\n", "feature");
        t.Git("checkout", "-q", "main");
        t.Commit("f.txt", "top\nmain\nbottom\n", "main");
        await t.Repo.Merge("feature");

        var b = Assert.Single(ConflictParser.Parse(t.Read("f.txt")).OfType<ConflictBlock>());
        Assert.Equal("main\n", b.Ours.Replace("\r\n", "\n"));
        Assert.Equal("feature\n", b.Theirs.Replace("\r\n", "\n"));
    }
}
