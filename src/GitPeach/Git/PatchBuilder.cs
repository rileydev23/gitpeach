using System.Text;
using GitPeach.Models;

namespace GitPeach.Git;

public static class PatchBuilder
{
    /// <summary>Builds a patch containing a single hunk of a file (apply with --cached [-R]).</summary>
    public static string ForHunk(DiffFile file, DiffHunk hunk)
    {
        var sb = new StringBuilder();
        foreach (var h in file.HeaderLines) sb.Append(h).Append('\n');
        AppendHunk(sb, hunk);
        return sb.ToString();
    }

    private static void AppendHunk(StringBuilder sb, DiffHunk hunk)
    {
        sb.Append(hunk.Header).Append('\n');
        foreach (var l in hunk.Lines)
        {
            if (l.Kind == DiffLineKind.NoNewline) sb.Append(l.Text);
            else sb.Append(l.Prefix).Append(l.Text);
            sb.Append('\n');
        }
    }
}
