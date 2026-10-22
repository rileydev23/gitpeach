using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>"Open in TextPeach": launches it, or explains that it is missing and offers its repository.</summary>
public static class TextPeachLink
{
    public static async Task Open(IShellService shell, IDialogService dialogs, string folder, string? file = null)
    {
        if (shell.OpenInTextPeach(folder, file)) return;
        var go = await dialogs.Confirm(Loc.T("textpeach.missing.title"), Loc.T("textpeach.missing.text"),
            Loc.T("textpeach.missing.go"), Loc.T("textpeach.missing.later"));
        if (go) shell.OpenUrl(TextPeachLocator.RepoUrl);
    }
}
