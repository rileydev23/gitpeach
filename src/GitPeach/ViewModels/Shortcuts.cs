namespace GitPeach.ViewModels;

/// <summary>Single source for the keyboard shortcuts shown in the help dialog (F1).</summary>
public static class Shortcuts
{
    public static readonly (string Gesture, string Description)[] Global =
    {
        ("Ctrl+P", "Abrir repositorio (búsqueda rápida)"),
        ("Ctrl+Tab / Ctrl+Shift+Tab", "Pestaña siguiente / anterior"),
        ("Ctrl+1 … Ctrl+9", "Ir a la pestaña N"),
        ("Ctrl+W", "Cerrar pestaña"),
        ("F1", "Acerca de y atajos de teclado"),
    };

    public static readonly (string Gesture, string Description)[] Repo =
    {
        ("F5", "Refrescar"),
        ("Ctrl+F", "Buscar en el historial"),
        ("Ctrl+Shift+F", "Fetch"),
        ("Ctrl+Shift+L", "Pull"),
        ("Ctrl+Shift+P", "Push"),
        ("Ctrl+B", "Nueva rama"),
        ("Ctrl+Shift+S", "Stash"),
        ("Ctrl+Shift+O", "Pop del último stash"),
        ("Ctrl+Shift+A", "Stage de todo"),
        ("Ctrl+Shift+U", "Unstage de todo"),
        ("Ctrl+Enter", "Commit"),
        ("Ctrl+L", "Mostrar / ocultar el log de comandos"),
        ("Esc (en la búsqueda)", "Limpiar la búsqueda"),
    };

    public const string AboutText = "Cliente de git para escritorio: historial con grafo, cambios, diffs, ramas, stashes y remotos de varios repositorios en pestañas.";

    public static (string Title, (string Gesture, string Description)[] Items)[] Groups =>
        new[] { ("General", Global), ("Repositorio", Repo) };
}
