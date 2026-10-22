using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>Single source for the keyboard shortcuts shown in the help dialog (F1).</summary>
public static class Shortcuts
{
    public static (string Gesture, string Description)[] Global =>
    [
        ("Ctrl+P", Loc.T("keys.quickOpen")),
        ("Ctrl+Tab / Ctrl+Shift+Tab", Loc.T("keys.nextTab")),
        ("Ctrl+1 … Ctrl+9", Loc.T("keys.tabN")),
        ("Ctrl+W", Loc.T("keys.closeTab")),
        ("F1", Loc.T("keys.about")),
    ];

    public static (string Gesture, string Description)[] Repo =>
    [
        ("F5", Loc.T("keys.refresh")),
        ("Ctrl+F", Loc.T("keys.search")),
        ("Ctrl+Shift+F", "Fetch"),
        ("Ctrl+Shift+L", "Pull"),
        ("Ctrl+Shift+P", "Push"),
        ("Ctrl+B", Loc.T("keys.newBranch")),
        ("Ctrl+Shift+S", "Stash"),
        ("Ctrl+Shift+O", Loc.T("keys.pop")),
        ("Ctrl+Shift+A", Loc.T("keys.stageAll")),
        ("Ctrl+Shift+U", Loc.T("keys.unstageAll")),
        ("Ctrl+Enter", "Commit"),
        ("Ctrl+L", Loc.T("keys.log")),
        (Loc.T("keys.escSearch"), Loc.T("keys.clearSearch")),
        (Loc.T("keys.doubleClick"), Loc.T("keys.doubleClick.desc")),
    ];

    public static (string Gesture, string Description)[] Viewer =>
    [
        ("Ctrl+E", Loc.T("keys.edit")),
        ("Ctrl+S", Loc.T("keys.save")),
        ("Esc", Loc.T("keys.closeWindow")),
    ];

    public static (string Title, (string Gesture, string Description)[] Items)[] Groups =>
        [(Loc.T("keys.group.general"), Global), (Loc.T("keys.group.repo"), Repo), (Loc.T("keys.group.viewer"), Viewer)];
}
