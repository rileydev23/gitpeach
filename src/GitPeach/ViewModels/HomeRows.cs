using System.Windows.Input;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>A row of the repository list: a group header or a repository.</summary>
public abstract class HomeRow;

public sealed class GroupHeaderRow : HomeRow
{
    /// <summary>Null for the "Sin grupo" section.</summary>
    public required RepoGroup? Group { get; init; }
    public required int Count { get; init; }
    public required bool IsCollapsed { get; init; }
    public required ICommand ToggleCommand { get; init; }
    public required ICommand OpenAllCommand { get; init; }

    public string Name => Group?.Name ?? "Sin grupo";
    public string Chevron => IsCollapsed ? "▸" : "▾";
    public string CountText => Count == 1 ? "1 repositorio" : $"{Count} repositorios";
    public bool CanOpenAll => Count > 0;
}

public sealed class RepoRow : HomeRow
{
    public required RepoEntry Entry { get; init; }
    /// <summary>Repos inside a group are indented under its header.</summary>
    public required bool Indented { get; init; }
    public Avalonia.Thickness Pad => new(Indented ? 26 : 10, 8, 10, 8);
}
