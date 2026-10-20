using GitPeach.Git;
using GitPeach.Models;
using GitPeach.Services;
using GitPeach.ViewModels;

namespace GitPeach.Tests;

public class GitignorePatternsTests
{
    [Theory]
    [InlineData("bin/out.log", "/bin/out.log")]
    [InlineData(@"dir\sub\file.txt", "/dir/sub/file.txt")]
    [InlineData("#notes.md", "/\\#notes.md")]
    [InlineData("weird[1].txt", "/weird\\[1].txt")]
    public void Path_patterns_are_anchored_and_escaped(string path, string expected)
        => Assert.Equal(expected, GitignorePatterns.ForPath(path));

    [Fact]
    public void Extension_pattern()
    {
        Assert.Equal("*.log", GitignorePatterns.ForExtension("a/b/c.log"));
        Assert.Null(GitignorePatterns.ForExtension("Makefile"));
    }
}

public class FuzzyMatcherTests
{
    [Theory]
    [InlineData("GitPeach C:/src/gitpeach", "gp", true)]
    [InlineData("GitPeach", "GPH", true)]
    [InlineData("GitPeach", "pg", false)]
    [InlineData("anything", "", true)]
    public void Matches_characters_in_order(string text, string query, bool expected)
        => Assert.Equal(expected, FuzzyMatcher.Matches(text, query));
}

public class MenuListTests
{
    [Fact]
    public void Clean_removes_leading_trailing_and_double_separators()
    {
        var a = MenuAction.Sync("A", () => { });
        var b = MenuAction.Sync("B", () => { });
        var cleaned = MenuList.Clean(new[] { MenuAction.Separator, a, MenuAction.Separator, MenuAction.Separator, b, MenuAction.Separator });
        Assert.Equal(new[] { "A", "-", "B" }, cleaned.Select(m => m.Header));
    }
}

public class BlameRowsTests
{
    [Fact]
    public void Groups_consecutive_lines_of_the_same_commit()
    {
        BlameLine L(string h, int n) => new() { Hash = h.PadRight(40, h[0]), LineNo = n, Text = "" };
        var rows = BlameViewModel.BuildRows(new[] { L("a", 1), L("a", 2), L("b", 3), L("a", 4) });

        Assert.Equal(new[] { true, false, true, true }, rows.Select(r => r.IsGroupStart));
        Assert.Equal("", rows[1].ShortHash);   // info only on the first line of a group
        Assert.NotSame(rows[0].Background, rows[2].Background);
    }
}

public class RepoWatcherTests
{
    [Theory]
    [InlineData(@"C:\r\.git\objects\ab\cdef", true)]
    [InlineData(@"C:\r\.git\index.lock", true)]
    [InlineData(@"C:\r\src\file.cs", false)]
    [InlineData(@"C:\r\.git\HEAD", false)]
    public void Ignores_git_internal_noise(string path, bool noise)
        => Assert.Equal(noise, RepoWatcher.IsNoise(@"C:\r", path));
}
