using Avalonia.Media;

namespace GitPeach.Controls;

public static class Palette
{
    private static SolidColorBrush B(string hex) => new(Color.Parse(hex)) ;

    public static readonly IBrush Bg = B("#1e1f22");
    public static readonly IBrush Panel = B("#26272b");
    public static readonly IBrush Panel2 = B("#2d2e33");
    public static readonly IBrush Border = B("#3a3c42");
    public static readonly IBrush Text = B("#d4d4d4");
    public static readonly IBrush Dim = B("#8a8d93");
    public static readonly IBrush Accent = B("#4a90d9");

    public static readonly IBrush AddedBg = B("#1d3a2a");
    public static readonly IBrush AddedFg = B("#a6e3b4");
    public static readonly IBrush RemovedBg = B("#431f22");
    public static readonly IBrush RemovedFg = B("#f4a6a6");
    public static readonly IBrush HunkBg = B("#25364d");
    public static readonly IBrush FileBg = B("#34363c");
    public static readonly IBrush ContextFg = B("#c8c8c8");
    public static readonly IBrush Transparent = Brushes.Transparent;

    public static readonly IBrush RefLocal = B("#2f6f3e");
    public static readonly IBrush RefRemote = B("#2d5c8a");
    public static readonly IBrush RefTag = B("#8a6d1f");
    public static readonly IBrush RefHead = B("#7a3b8a");

    public static readonly Color[] LaneColors =
    {
        Color.Parse("#4a90d9"), Color.Parse("#e0a24a"), Color.Parse("#6cc070"), Color.Parse("#d96a9b"),
        Color.Parse("#9a7ae0"), Color.Parse("#4ac7c0"), Color.Parse("#e0645a"), Color.Parse("#c8c84a"),
    };

    public static IBrush StatusBrush(char s) => s switch
    {
        'A' or '?' => B("#6cc070"),
        'D' => B("#e0645a"),
        'U' => B("#e0a24a"),
        'R' or 'C' => B("#9a7ae0"),
        _ => B("#4a90d9"),
    };
}
