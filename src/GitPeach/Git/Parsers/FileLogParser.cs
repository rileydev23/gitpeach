using GitPeach.Models;

namespace GitPeach.Git.Parsers;

public static class FileLogParser
{
    /// <summary>
    /// Format for `git log --follow --name-only`: each record starts with RS (0x1e) so the
    /// path printed by --name-only after it can be told apart from the header.
    /// </summary>
    public const string Format = "--format=%x1e%H%x1f%P%x1f%an%x1f%ae%x1f%at%x1f%s";

    public static List<FileRevision> Parse(string output, string fallbackPath)
    {
        var list = new List<FileRevision>();
        foreach (var rec in output.Split('\x1e', StringSplitOptions.RemoveEmptyEntries))
        {
            var lines = rec.Split('\n');
            var f = lines[0].TrimEnd('\r').Split('\x1f');
            if (f.Length < 6) continue;
            long.TryParse(f[4], out var ts);
            var path = lines.Skip(1).Select(l => l.TrimEnd('\r')).FirstOrDefault(l => l.Length > 0) ?? fallbackPath;
            var commit = new Commit
            {
                Hash = f[0],
                Parents = f[1].Length == 0 ? Array.Empty<string>() : f[1].Split(' '),
                Author = f[2],
                Email = f[3],
                Date = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime(),
                Subject = f[5],
                Message = f[5],
                Refs = Array.Empty<RefLabel>(),
            };
            list.Add(new FileRevision(commit, path));
        }
        return list;
    }
}
