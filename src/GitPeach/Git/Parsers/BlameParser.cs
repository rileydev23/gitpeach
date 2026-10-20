using GitPeach.Models;

namespace GitPeach.Git.Parsers;

public static class BlameParser
{
    private sealed class Info
    {
        public string Author = "";
        public long Time;
        public string Summary = "";
    }

    /// <summary>
    /// Parses `git blame --porcelain`. Commit details are printed only the first time a commit
    /// appears, so they are remembered by hash for the following lines.
    /// </summary>
    public static List<BlameLine> Parse(string output)
    {
        var result = new List<BlameLine>();
        var infos = new Dictionary<string, Info>();
        string? hash = null;
        int finalLine = 0;

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.EndsWith('\r') ? raw[..^1] : raw;
            if (line.StartsWith('\t'))
            {
                if (hash is null) continue;
                var info = infos[hash];
                result.Add(new BlameLine
                {
                    Hash = hash,
                    LineNo = finalLine,
                    Text = line[1..],
                    Author = info.Author,
                    Date = DateTimeOffset.FromUnixTimeSeconds(info.Time).ToLocalTime(),
                    Summary = info.Summary,
                });
                hash = null;
                continue;
            }

            if (hash is null)
            {
                // Header: "<hash> <orig-line> <final-line> [<group-size>]"
                var f = line.Split(' ');
                if (f.Length >= 3 && f[0].Length == 40 && int.TryParse(f[2], out var n))
                {
                    hash = f[0];
                    finalLine = n;
                    if (!infos.ContainsKey(hash)) infos[hash] = new Info();
                }
                continue;
            }

            var cur = infos[hash];
            if (line.StartsWith("author ")) cur.Author = line[7..];
            else if (line.StartsWith("author-time ")) long.TryParse(line[12..], out cur.Time);
            else if (line.StartsWith("summary ")) cur.Summary = line[8..];
        }
        return result;
    }
}
