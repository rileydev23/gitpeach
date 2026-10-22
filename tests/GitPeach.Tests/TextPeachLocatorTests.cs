using GitPeach.Services;

namespace GitPeach.Tests;

// Native paths and the platform's executable name, so the tests also hold on Linux.
public class TextPeachLocatorTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "peach");
    private static readonly string Apps = Path.Combine(Root, "Apps");
    private static readonly string Base = Path.Combine(Apps, "GitPeach");
    private static readonly string Exe = TextPeachLocator.ExeName;

    [Fact]
    public void PrefersTheCopyNextToGitPeach()
    {
        var tools = Path.Combine(Root, "Tools");
        var nextTo = Path.Combine(Base, Exe);
        var found = TextPeachLocator.Find(p => p == nextTo || p == Path.Combine(tools, Exe), Base, null, null, tools);
        Assert.Equal(nextTo, found);
    }

    [Fact]
    public void FallsBackToSiblingFolderInstallFoldersAndPath()
    {
        var sibling = Path.Combine(Apps, "TextPeach", Exe);
        Assert.Equal(sibling, TextPeachLocator.Find(p => p == sibling, Base, null, null, null));

        var local = Path.Combine(Root, "Local");
        Assert.Equal(Path.Combine(local, "Programs", "TextPeach", Exe),
            TextPeachLocator.Find(p => p.Contains("Programs"), Base, local, null, null));

        var bin = Path.Combine(Root, "bin");
        var inPath = Path.Combine(bin, Exe);
        var path = $"{Path.Combine(Root, "x")}{Path.PathSeparator}\"{bin}\"{Path.PathSeparator}";
        Assert.Equal(inPath, TextPeachLocator.Find(p => p == inPath, Base, null, null, path));
    }

    [Fact]
    public void ReturnsNullWhenMissing()
        => Assert.Null(TextPeachLocator.Find(_ => false, Base, Path.Combine(Root, "L"), Path.Combine(Root, "P"),
            $"{Path.Combine(Root, "a")}{Path.PathSeparator}{Path.Combine(Root, "b")}"));
}
