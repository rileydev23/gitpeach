namespace GitPeach.Services;

/// <summary>
/// Finds TextPeach (the read-only code viewer of the same family) so repositories and files can be
/// opened in it. Pure: the file system and environment are passed in, which keeps it testable.
/// </summary>
public static class TextPeachLocator
{
    public const string ExeName = "TextPeach.exe";
    public const string RepoUrl = "https://github.com/devrileywin/TextPeach";

    /// <summary>
    /// Candidate locations in priority order: next to GitPeach, a sibling "TextPeach" folder,
    /// the usual per-user and per-machine install folders, then every folder in PATH.
    /// </summary>
    public static IEnumerable<string> Candidates(string baseDir, string? localAppData, string? programFiles, string? pathVariable)
    {
        yield return Path.Combine(baseDir, ExeName);
        var parent = Path.GetDirectoryName(baseDir.TrimEnd('\\', '/'));
        if (parent is not null) yield return Path.Combine(parent, "TextPeach", ExeName);
        if (!string.IsNullOrEmpty(localAppData)) yield return Path.Combine(localAppData, "Programs", "TextPeach", ExeName);
        if (!string.IsNullOrEmpty(programFiles)) yield return Path.Combine(programFiles, "TextPeach", ExeName);
        foreach (var dir in (pathVariable ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            yield return Path.Combine(dir.Trim('"'), ExeName);
    }

    public static string? Find(Func<string, bool> exists, string baseDir, string? localAppData, string? programFiles, string? pathVariable)
    {
        foreach (var c in Candidates(baseDir, localAppData, programFiles, pathVariable))
        {
            try { if (exists(c)) return c; } catch { /* malformed PATH entry */ }
        }
        return null;
    }

    public static string? FindOnThisMachine() => Find(File.Exists,
        AppContext.BaseDirectory,
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
        Environment.GetEnvironmentVariable("PATH"));
}
