using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>The rows shown by a DiffListView. Shared by the repository tab and the file-history window.</summary>
public sealed partial class DiffPaneViewModel : ObservableObject
{
    public static IReadOnlyList<DiffRowVM> Loading => new[] { DiffRowBuilder.Note(Loc.T("diff.loading")) };

    // Swapped as a whole (one UI update) instead of cleared and refilled row by row.
    [ObservableProperty] private IReadOnlyList<DiffRowVM> _rows = Array.Empty<DiffRowVM>();

    /// <summary>Opens this diff in its own window; null hides the button.</summary>
    public ICommand? PopOutCommand { get; init; }
    public bool CanPopOut => PopOutCommand is not null;

    public event Action<DiffRowVM>? ScrollToRowRequested;

    public void ScrollToFile(string path)
    {
        var row = Rows.FirstOrDefault(r => r.IsFileHeader && r.File?.DisplayPath == path);
        if (row is not null) ScrollToRowRequested?.Invoke(row);
    }

    public void Clear() => Rows = Array.Empty<DiffRowVM>();
}
