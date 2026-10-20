using GitPeach.Services;

namespace GitPeach.Tests;

public class TextPeachLocatorTests
{
    private const string Base = @"C:\Apps\GitPeach";

    [Fact]
    public void PrefersTheCopyNextToGitPeach()
    {
        var found = TextPeachLocator.Find(p => p is @"C:\Apps\GitPeach\TextPeach.exe" or @"C:\Tools\TextPeach.exe",
            Base, null, null, @"C:\Tools");
        Assert.Equal(@"C:\Apps\GitPeach\TextPeach.exe", found);
    }

    [Fact]
    public void FallsBackToSiblingFolderInstallFoldersAndPath()
    {
        Assert.Equal(@"C:\Apps\TextPeach\TextPeach.exe",
            TextPeachLocator.Find(p => p == @"C:\Apps\TextPeach\TextPeach.exe", Base, null, null, null));
        Assert.Equal(@"C:\Users\u\AppData\Local\Programs\TextPeach\TextPeach.exe",
            TextPeachLocator.Find(p => p.Contains("Programs"), Base, @"C:\Users\u\AppData\Local", null, null));
        Assert.Equal(@"D:\bin\TextPeach.exe",
            TextPeachLocator.Find(p => p == @"D:\bin\TextPeach.exe", Base, null, null, @"C:\x;""D:\bin"";"));
    }

    [Fact]
    public void ReturnsNullWhenMissing()
        => Assert.Null(TextPeachLocator.Find(_ => false, Base, @"C:\L", @"C:\P", @"C:\a;C:\b"));
}
