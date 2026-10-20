using GitPeach.Git;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>Everything that can be done to a single file: staging, conflicts, shell, .gitignore, history and blame.</summary>
public sealed class FileActions(
    IRepoSession session,
    IGitRepository git,
    IDialogService dialogs,
    IShellService shell,
    IClipboardService clipboard,
    IWindowService windows)
{
    private string FullPath(string relative) => Path.GetFullPath(Path.Combine(session.RepoPath, relative));

    // ---------- staging ----------

    public Task Stage(IEnumerable<string> paths) => session.PerformQuiet(() => git.Stage(paths));
    public Task Unstage(IEnumerable<string> paths) => session.PerformQuiet(() => git.Unstage(paths));

    public async Task Discard(FileChange change)
    {
        var what = change.IsUntracked ? "Se eliminará el archivo sin seguimiento" : "Se perderán los cambios";
        if (await dialogs.Confirm("Descartar cambios", $"{what}: {change.Path}\n\nEsta acción no se puede deshacer.", "Descartar"))
            await session.PerformQuiet(() => git.Discard(change));
    }

    // ---------- conflicts ----------

    /// <summary>
    /// Labels for --ours / --theirs. During a rebase git swaps them: "ours" is the branch being
    /// rebased onto and "theirs" is your commit being replayed.
    /// </summary>
    public (string Ours, string Theirs) ConflictLabels() => session.Snapshot.Operation == RepoOperation.Rebase
        ? ("Usar la versión de la base (ours)", "Usar la versión de tu commit (theirs)")
        : ("Usar mi versión (ours / HEAD)", "Usar la versión entrante (theirs)");

    public async Task UseSide(FileChange change, ConflictSide side)
    {
        await session.PerformQuiet(async () =>
        {
            var r = await git.UseConflictSide(change.Path, side);
            // The chosen side deleted the file: resolving means removing it.
            if (!r.Ok && r.Message.Contains("does not have", StringComparison.OrdinalIgnoreCase) &&
                await dialogs.Confirm("Archivo eliminado en esa versión",
                    $"En esa versión '{change.Path}' fue eliminado. ¿Resolver el conflicto eliminando el archivo?", "Eliminar"))
                r = await git.RemoveConflicted(change.Path);
            return r;
        });
    }

    public Task MarkResolved(IEnumerable<string> paths) => session.PerformQuiet(() => git.MarkResolved(paths));

    public async Task OpenMergeTool(FileChange change)
    {
        session.Notify($"Herramienta de merge abierta para {change.Path}…");
        var r = await git.MergeTool(change.Path);
        if (!r.Ok)
            await dialogs.Message("Herramienta de merge",
                r.Message + "\n\nConfigura una con:  git config --global merge.tool <herramienta>\n" +
                "o resuelve el archivo en tu editor y usa \"Marcar como resuelto\".");
        await session.RefreshAsync(false);
    }

    // ---------- .gitignore ----------

    public async Task Ignore(string pattern)
    {
        await session.PerformQuiet(() => git.AddToGitignore(pattern));
        session.Notify($"Agregado a .gitignore: {pattern}");
    }

    // ---------- windows ----------

    public void ShowHistory(string path) => windows.Show(new FileHistoryViewModel(git, path, this));

    public void ShowBlame(string path, string? revision = null) => windows.Show(new BlameViewModel(git, path, revision));

    // ---------- menus ----------

    public IReadOnlyList<MenuAction> MenuFor(FileEntryVM entry)
    {
        var c = entry.Change;
        var full = FullPath(c.Path);
        var exists = File.Exists(full);
        var items = new List<MenuAction>();

        if (c.IsConflict)
        {
            var (ours, theirs) = ConflictLabels();
            items.Add(new MenuAction(ours, () => UseSide(c, ConflictSide.Ours)));
            items.Add(new MenuAction(theirs, () => UseSide(c, ConflictSide.Theirs)));
            items.Add(new MenuAction("Marcar como resuelto", () => MarkResolved(new[] { c.Path })));
            items.Add(new MenuAction("Abrir herramienta de merge", () => OpenMergeTool(c)));
            items.Add(MenuAction.Separator);
        }
        else if (c.Staged)
        {
            items.Add(new MenuAction("Unstage", () => Unstage(new[] { c.Path })));
        }
        else
        {
            items.Add(new MenuAction("Stage", () => Stage(new[] { c.Path })));
            items.Add(new MenuAction("Descartar cambios…", () => Discard(c)));
        }

        items.Add(MenuAction.Separator);
        items.AddRange(ShellItems(c.Path, exists));

        if (c.IsUntracked)
        {
            items.Add(MenuAction.Separator);
            items.Add(new MenuAction("Ignorar este archivo (.gitignore)", () => Ignore(GitignorePatterns.ForPath(c.Path))));
            if (GitignorePatterns.ForExtension(c.Path) is { } ext)
                items.Add(new MenuAction($"Ignorar todos los {ext}", () => Ignore(ext)));
        }
        else if (c.Status != 'A')
        {
            items.Add(MenuAction.Separator);
            items.Add(MenuAction.Sync("Historial del archivo", () => ShowHistory(c.Path)));
            items.Add(MenuAction.Sync("Blame", () => ShowBlame(c.Path)));
        }
        return MenuList.Clean(items);
    }

    public IReadOnlyList<MenuAction> MenuFor(CommitFileVM file, Commit commit)
    {
        var exists = File.Exists(FullPath(file.Path));
        var items = new List<MenuAction>();
        items.AddRange(ShellItems(file.Path, exists));
        items.Add(MenuAction.Separator);
        items.Add(MenuAction.Sync("Historial del archivo", () => ShowHistory(file.Path)));
        if (file.Status != 'D')
            items.Add(MenuAction.Sync("Blame en este commit", () => ShowBlame(file.Path, commit.Hash)));
        return MenuList.Clean(items);
    }

    private IEnumerable<MenuAction> ShellItems(string relative, bool exists)
    {
        var full = FullPath(relative);
        if (exists)
        {
            yield return MenuAction.Sync("Abrir archivo", () => shell.OpenFile(full));
            yield return new MenuAction("Abrir en TextPeach", () => TextPeachLink.Open(shell, dialogs, session.RepoPath, full));
        }
        yield return MenuAction.Sync("Mostrar en el Explorador", () => shell.RevealInExplorer(full));
        yield return new MenuAction("Copiar ruta relativa", () => clipboard.SetTextAsync(relative.Replace('\\', '/')));
        yield return new MenuAction("Copiar ruta completa", () => clipboard.SetTextAsync(full));
    }
}
