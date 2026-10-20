using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using GitPeach.Controls;
using GitPeach.Git;
using GitPeach.Models;

namespace GitPeach.ViewModels;

/// <summary>Line-by-line authorship of a file, at HEAD/working tree or at a given commit.</summary>
public sealed partial class BlameViewModel : ObservableObject
{
    public string FilePath { get; }
    public string? Revision { get; }
    public string Title => Revision is null ? $"Blame — {FilePath}" : $"Blame — {FilePath} @ {Revision[..Math.Min(8, Revision.Length)]}";

    [ObservableProperty] private IReadOnlyList<BlameRowVM> _rows = Array.Empty<BlameRowVM>();
    [ObservableProperty] private string _status = "Cargando blame…";

    public BlameViewModel(IGitQueries git, string path, string? revision)
    {
        FilePath = path.Replace('\\', '/');
        Revision = revision;
        _ = LoadAsync(git);
    }

    private async Task LoadAsync(IGitQueries git)
    {
        var lines = await git.Blame(FilePath, Revision);
        Rows = await Task.Run(() => BuildRows(lines));
        var commits = lines.Select(l => l.Hash).Distinct().Count();
        Status = lines.Count == 0
            ? "No se pudo obtener el blame (¿archivo binario o sin commits?)."
            : $"{lines.Count} líneas · {commits} commit{(commits == 1 ? "" : "s")}";
    }

    /// <summary>Consecutive lines from the same commit form a group; groups alternate background.</summary>
    public static List<BlameRowVM> BuildRows(IReadOnlyList<BlameLine> lines)
    {
        var rows = new List<BlameRowVM>(lines.Count);
        string? prev = null;
        bool odd = false;
        foreach (var l in lines)
        {
            bool start = l.Hash != prev;
            if (start && prev is not null) odd = !odd;
            prev = l.Hash;
            rows.Add(new BlameRowVM { Line = l, IsGroupStart = start, Background = odd ? Palette.Panel : Brushes.Transparent });
        }
        return rows;
    }
}
