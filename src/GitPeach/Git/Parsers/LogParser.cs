using GitPeach.Models;

namespace GitPeach.Git.Parsers;

public static class LogParser
{
    /// <summary>Format passed to git log; records are NUL-separated (use with -z).</summary>
    public const string Format = "--format=%H%x1f%P%x1f%an%x1f%ae%x1f%at%x1f%D%x1f%s%x1f%cn%x1f%ce%x1f%ct%x1f%B";

    public static List<Commit> Parse(string output)
    {
        var list = new List<Commit>();
        foreach (var rec in output.Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var f = rec.TrimStart('\n').Split('\x1f');
            if (f.Length < 7) continue;
            long.TryParse(f[4], out var ts);
            bool full = f.Length >= 11;
            long.TryParse(full ? f[9] : "0", out var cts);
            list.Add(new Commit
            {
                Hash = f[0],
                Parents = f[1].Length == 0 ? Array.Empty<string>() : f[1].Split(' '),
                Author = f[2],
                Email = f[3],
                Date = DateTimeOffset.FromUnixTimeSeconds(ts).ToLocalTime(),
                Refs = ParseRefs(f[5]),
                Subject = f[6],
                Committer = full ? f[7] : "",
                CommitterEmail = full ? f[8] : "",
                CommitDate = DateTimeOffset.FromUnixTimeSeconds(cts).ToLocalTime(),
                Message = full ? f[10].TrimEnd() : f[6],
            });
        }
        return list;
    }

    public static List<RefLabel> ParseRefs(string decorations)
    {
        var refs = new List<RefLabel>();
        if (string.IsNullOrWhiteSpace(decorations)) return refs;
        foreach (var raw in decorations.Split(", ", StringSplitOptions.RemoveEmptyEntries))
        {
            var d = raw.Trim();
            if (d.StartsWith("HEAD -> "))
            {
                refs.Add(new RefLabel(d[8..], RefKind.LocalBranch, true));
            }
            else if (d == "HEAD")
            {
                refs.Add(new RefLabel("HEAD", RefKind.Head, true));
            }
            else if (d.StartsWith("tag: "))
            {
                refs.Add(new RefLabel(d[5..], RefKind.Tag));
            }
            else if (d.EndsWith("/HEAD"))
            {
                continue;
            }
            else if (d.Contains('/'))
            {
                refs.Add(new RefLabel(d, RefKind.RemoteBranch));
            }
            else
            {
                refs.Add(new RefLabel(d, RefKind.LocalBranch));
            }
        }
        return refs;
    }
}
