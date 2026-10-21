using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>"Abrir en TextPeach": launches it, or explains that it is missing and offers its repository.</summary>
public static class TextPeachLink
{
    public static async Task Open(IShellService shell, IDialogService dialogs, string folder, string? file = null)
    {
        if (shell.OpenInTextPeach(folder, file)) return;
        var go = await dialogs.Confirm("TextPeach no está instalado",
            "TextPeach es el visor de código de solo lectura que acompaña a GitPeach, y no se encontró en este equipo.\n\n" +
            "Si ya lo tienes, ábrelo una vez desde donde esté y GitPeach lo reconocerá solo. ¿Quieres ir a su repositorio para descargarlo?",
            "Ir al repositorio", "Por ahora no");
        if (go) shell.OpenUrl(TextPeachLocator.RepoUrl);
    }
}
