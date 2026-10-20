using System.Text.RegularExpressions;
using GitPeach.Models;

namespace GitPeach.Git.Parsers;

public static partial class DiffParser
{
    [GeneratedRegex(@"^@@ -(\d+)(?:,(\d+))? \+(\d+)(?:,(\d+))? @@ ?(.*)$")]
    private static partial Regex HunkRegex();

    public static List<DiffFile> Parse(string diff)
    {
        var files = new List<DiffFile>();
        if (string.IsNullOrEmpty(diff)) return files;

        DiffFile? file = null;
        DiffHunk? hunk = null;
        int oldNo = 0, newNo = 0;

        foreach (var raw in diff.Split('\n'))
        {
            var line = raw.EndsWith('\r') ? raw[..^1] : raw;

            if (line.StartsWith("diff --git "))
            {
                file = new DiffFile();
                files.Add(file);
                hunk = null;
                file.HeaderLines.Add(line);
                ParseGitHeaderPaths(line, file);
                continue;
            }
            if (file is null) continue;

            if (hunk is null)
            {
                if (line.StartsWith("@@"))
                {
                    // fallthrough to hunk handling below
                }
                else
                {
                    file.HeaderLines.Add(line);
                    if (line.StartsWith("new file mode")) file.IsNew = true;
                    else if (line.StartsWith("deleted file mode")) file.IsDeleted = true;
                    else if (line.StartsWith("Binary files ") || line.StartsWith("GIT binary patch")) file.IsBinary = true;
                    else if (line.StartsWith("--- ")) file.OldPath = StripPrefix(line[4..], "a/");
                    else if (line.StartsWith("+++ ")) file.NewPath = StripPrefix(line[4..], "b/");
                    else if (line.StartsWith("rename from ")) file.OldPath = line[12..];
                    else if (line.StartsWith("rename to ")) file.NewPath = line[10..];
                    continue;
                }
            }

            var m = HunkRegex().Match(line);
            if (m.Success)
            {
                int os = int.Parse(m.Groups[1].Value);
                int oc = m.Groups[2].Success ? int.Parse(m.Groups[2].Value) : 1;
                int ns = int.Parse(m.Groups[3].Value);
                int nc = m.Groups[4].Success ? int.Parse(m.Groups[4].Value) : 1;
                hunk = new DiffHunk
                {
                    Header = line,
                    OldStart = os, OldCount = oc, NewStart = ns, NewCount = nc,
                    Context = m.Groups[5].Value,
                };
                file.Hunks.Add(hunk);
                oldNo = os; newNo = ns;
                continue;
            }
            if (hunk is null) continue;

            if (line.StartsWith('+'))
                hunk.Lines.Add(new DiffLine { Kind = DiffLineKind.Added, Text = line[1..], NewNo = newNo++ });
            else if (line.StartsWith('-'))
                hunk.Lines.Add(new DiffLine { Kind = DiffLineKind.Removed, Text = line[1..], OldNo = oldNo++ });
            else if (line.StartsWith('\\'))
                hunk.Lines.Add(new DiffLine { Kind = DiffLineKind.NoNewline, Text = line });
            else if (line.StartsWith(' '))
                hunk.Lines.Add(new DiffLine { Kind = DiffLineKind.Context, Text = line[1..], OldNo = oldNo++, NewNo = newNo++ });
            // Anything else (empty trailing line) is ignored.
        }
        return files;
    }

    private static void ParseGitHeaderPaths(string line, DiffFile file)
    {
        // "diff --git a/x b/x" — only a fallback; ---/+++ and rename lines refine it.
        var rest = line["diff --git ".Length..];
        var idx = rest.IndexOf(" b/", StringComparison.Ordinal);
        if (rest.StartsWith("a/") && idx > 0)
        {
            file.OldPath = rest[2..idx];
            file.NewPath = rest[(idx + 3)..];
        }
    }

    private static string? StripPrefix(string p, string prefix)
    {
        if (p == "/dev/null") return null;
        return p.StartsWith(prefix) ? p[prefix.Length..] : p;
    }
}
