namespace GitPeach.Git;

/// <summary>Builds .gitignore lines for a repository-relative path.</summary>
public static class GitignorePatterns
{
    /// <summary>Anchored pattern that matches exactly this file or folder ("/dir/file.txt").</summary>
    public static string ForPath(string relativePath)
    {
        var p = relativePath.Replace('\\', '/').Trim('/');
        return "/" + Escape(p);
    }

    /// <summary>"*.ext" for the file's extension, or null when it has none.</summary>
    public static string? ForExtension(string relativePath)
    {
        var ext = Path.GetExtension(relativePath);
        return string.IsNullOrEmpty(ext) || ext == "." ? null : "*" + Escape(ext);
    }

    private static string Escape(string p)
    {
        // Glob characters must be literal; a trailing space would be stripped by git.
        var sb = new System.Text.StringBuilder(p.Length + 4);
        foreach (var ch in p)
        {
            if (ch is '*' or '?' or '[' or '\\' or '!' or '#') sb.Append('\\');
            sb.Append(ch);
        }
        if (sb.Length > 0 && sb[^1] == ' ') sb.Insert(sb.Length - 1, '\\');
        return sb.ToString();
    }
}
