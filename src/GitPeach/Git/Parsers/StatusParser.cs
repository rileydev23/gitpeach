using GitPeach.Models;

namespace GitPeach.Git.Parsers;

public static class StatusParser
{
    /// <summary>Parses `git status --porcelain=v2 -z --branch --untracked-files=all`.</summary>
    public static StatusInfo Parse(string output)
    {
        var info = new StatusInfo();
        var parts = output.Split('\0');
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p.Length == 0) continue;
            switch (p[0])
            {
                case '#':
                    ParseHeader(p, info);
                    break;
                case '1':
                {
                    // 1 XY sub mH mI mW hH hI path
                    var f = p.Split(' ', 9);
                    if (f.Length < 9) break;
                    AddEntry(info, f[1], f[8], null);
                    break;
                }
                case '2':
                {
                    // 2 XY sub mH mI mW hH hI Xscore path \0 origPath
                    var f = p.Split(' ', 10);
                    if (f.Length < 10) break;
                    var orig = i + 1 < parts.Length ? parts[++i] : null;
                    AddEntry(info, f[1], f[9], orig);
                    break;
                }
                case 'u':
                {
                    var f = p.Split(' ', 11);
                    if (f.Length < 11) break;
                    info.Unstaged.Add(new FileChange(f[10], null, 'U', false));
                    break;
                }
                case '?':
                    info.Unstaged.Add(new FileChange(p[2..], null, '?', false));
                    break;
            }
        }
        return info;
    }

    private static void AddEntry(StatusInfo info, string xy, string path, string? orig)
    {
        if (xy[0] != '.') info.Staged.Add(new FileChange(path, orig, xy[0], true));
        if (xy[1] != '.') info.Unstaged.Add(new FileChange(path, orig, xy[1], false));
    }

    private static void ParseHeader(string line, StatusInfo info)
    {
        if (line.StartsWith("# branch.oid "))
        {
            var v = line[13..];
            info.Oid = v == "(initial)" ? null : v;
        }
        else if (line.StartsWith("# branch.head "))
        {
            var v = line[14..];
            info.Branch = v == "(detached)" ? null : v;
        }
        else if (line.StartsWith("# branch.upstream "))
        {
            info.Upstream = line[18..];
        }
        else if (line.StartsWith("# branch.ab "))
        {
            var f = line[12..].Split(' ');
            if (f.Length == 2)
            {
                int.TryParse(f[0].TrimStart('+'), out var a);
                int.TryParse(f[1].TrimStart('-'), out var b);
                info.Ahead = a;
                info.Behind = b;
            }
        }
    }
}
