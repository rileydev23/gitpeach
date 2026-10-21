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

    /// <summary>GitPeach's own merge tool.</summary>
    public void ResolveConflict(string path) => windows.Show(new ConflictResolverViewModel(
        FullPath(path), path, session.Snapshot.Operation == RepoOperation.Rebase, this, dialogs));

    /// <summary>The user's configured merge.tool; without one (or if it fails) GitPeach's own tool opens instead.</summary>
    public async Task OpenMergeTool(FileChange change)
    {
        if (!await git.HasMergeTool())
        {
            session.Notify("No hay herramienta de merge externa configurada: se abre la de GitPeach");
            ResolveConflict(change.Path);
            return;
        }
        session.Notify($"Herramienta de merge abierta para {change.Path}…");
        var r = await git.MergeTool(change.Path);
        await session.RefreshAsync(false);
        if (!r.Ok && await dialogs.Confirm("Herramienta de merge",
                r.Message + "\n\n¿Resolverlo con la herramienta de GitPeach?", "Resolver en GitPeach"))
            ResolveConflict(change.Path);
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

    /// <summary>Built-in viewer (read-only until "Editar"). <paramref name="edit"/> opens it already editing.</summary>
    public void ViewFile(string path, bool edit = false) =>
        windows.Show(new FileViewerViewModel(FullPath(path), path, dialogs, shell, edit));

    public void OpenWithDefaultApp(string path) => shell.OpenFile(FullPath(path));

    /// <summary>Double-click on a working-tree file: conflicts open the resolver, other files the viewer.</summary>
    public void Activate(FileChange change)
    {
        if (change.IsConflict) ResolveConflict(change.Path);
        else if (File.Exists(FullPath(change.Path))) ViewFile(change.Path);
    }

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
            items.Add(MenuAction.Sync("Resolver conflicto…", () => ResolveConflict(c.Path)));
            items.Add(MenuAction.Separator);
            items.Add(new MenuAction(ours, () => UseSide(c, ConflictSide.Ours)));
            items.Add(new MenuAction(theirs, () => UseSide(c, ConflictSide.Theirs)));
            items.Add(new MenuAction("Marcar como resuelto", () => MarkResolved(new[] { c.Path })));
            items.Add(new MenuAction("Herramienta de merge externa", () => OpenMergeTool(c)));
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
            yield return MenuAction.Sync("Ver / editar archivo", () => ViewFile(relative));
            yield return MenuAction.Sync("Abrir con la app predeterminada", () => shell.OpenFile(full));
            yield return new MenuAction("Abrir en TextPeach", () => TextPeachLink.Open(shell, dialogs, session.RepoPath, full));
        }
        yield return MenuAction.Sync("Mostrar en el Explorador", () => shell.RevealInExplorer(full));
        yield return new MenuAction("Copiar ruta relativa", () => clipboard.SetTextAsync(relative.Replace('\\', '/')));
        yield return new MenuAction("Copiar ruta completa", () => clipboard.SetTextAsync(full));
    }
}
