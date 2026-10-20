using GitPeach.Models;

namespace GitPeach.Git.Parsers;

public static class StashParser
{
    public const string Format = "--format=%gd%x1f%H%x1f%at%x1f%gs";

    public static List<StashInfo> Parse(string output)
    {
        var list = new List<StashInfo>();
        int i = 0;
        foreach (var line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = line.TrimEnd('\r').Split('\x1f');
            if (f.Length < 4) continue;
            long.TryParse(f[2], out var ts);
            list.Add(new StashInfo(i++, f[0], f[1], DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime(), f[3]));
        }
        return list;
    }
}
