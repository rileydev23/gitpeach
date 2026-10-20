using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Git;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>The "Repositorios" tab: saved repositories, organised in collapsible groups.</summary>
public partial class HomeViewModel : TabViewModel
{
    private readonly IAppStateStore _store;
    private readonly IGitHost _git;
    private readonly IDialogService _dialogs;
    private readonly IShellService _shell;

    /// <summary>Raised when the user wants to open a repository in a tab.</summary>
    public event Action<RepoEntry>? OpenRequested;

    public override bool Closable => false;
    public ObservableCollection<HomeRow> Rows { get; } = new();

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private bool _onlyFavorites;
    [ObservableProperty] private HomeRow? _selectedRow;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private bool _isEmpty;

    public RepoEntry? Selected => (SelectedRow as RepoRow)?.Entry;
    private AppState State => _store.State;
    private bool IsFiltering => OnlyFavorites || Filter.Trim().Length > 0;

    public HomeViewModel(IAppStateStore store, IGitHost git, IDialogService dialogs, IShellService shell)
    {
        _store = store;
        _git = git;
        _dialogs = dialogs;
        _shell = shell;
        Title = "Repositorios";
        Reload();
        _ = RefreshBranchesAsync();
    }

    partial void OnFilterChanged(string value) => Reload();
    partial void OnOnlyFavoritesChanged(bool value) => Reload();

    // ---------------------------------------------------------------- list

    public void Reload()
    {
        var keep = Selected;
        var q = Filter.Trim();
        bool filtering = IsFiltering;
        var sections = RepoGrouping.Build(State,
            r => (!OnlyFavorites || r.Favorite) && FuzzyMatcher.Matches(r.Name + " " + r.Path, q),
            hideEmpty: filtering);

        Rows.Clear();
        // With no groups at all the list stays flat, as before groups existed.
        bool headers = State.Groups.Count > 0;
        foreach (var section in sections)
        {
            // While filtering every section is expanded, so matches are never hidden.
            bool collapsed = !filtering && (section.Group?.Collapsed ?? State.UngroupedCollapsed);
            if (headers)
            {
                var s = section;
                Rows.Add(new GroupHeaderRow
                {
                    Group = s.Group,
                    Count = s.Repos.Count,
                    IsCollapsed = collapsed,
                    ToggleCommand = new RelayCommand(() => ToggleGroup(s.Group)),
                    OpenAllCommand = new RelayCommand(() => OpenAll(s.Repos)),
                });
            }
            if (collapsed) continue;
            foreach (var r in section.Repos) Rows.Add(new RepoRow { Entry = r, Indented = headers });
        }

        SelectedRow = keep is null ? null : Rows.OfType<RepoRow>().FirstOrDefault(r => r.Entry == keep);
        IsEmpty = !Rows.OfType<RepoRow>().Any() && !Rows.OfType<GroupHeaderRow>().Any(h => h.Count > 0);
    }

    public async Task RefreshBranchesAsync()
    {
        var snapshot = State.Repos.ToList();
        await Task.WhenAll(snapshot.Select(async r =>
        {
            r.Missing = !Directory.Exists(r.Path);
            r.Branch = r.Missing ? null : await _git.CurrentBranchOf(r.Path);
        }));
        Reload();
    }

    [RelayCommand]
    private async Task RefreshAsync() => await RefreshBranchesAsync();

    // ---------------------------------------------------------------- groups

    public void ToggleGroup(RepoGroup? group)
    {
        if (IsFiltering) return; // everything is expanded while filtering
        if (group is null) State.UngroupedCollapsed = !State.UngroupedCollapsed;
        else group.Collapsed = !group.Collapsed;
        _store.Save();
        Reload();
    }

    [RelayCommand]
    private void CollapseAll() => SetAllCollapsed(true);

    [RelayCommand]
    private void ExpandAll() => SetAllCollapsed(false);

    private void SetAllCollapsed(bool collapsed)
    {
        foreach (var g in State.Groups) g.Collapsed = collapsed;
        State.UngroupedCollapsed = collapsed;
        _store.Save();
        Reload();
    }

    private void OpenAll(IReadOnlyList<RepoEntry> repos)
    {
        var existing = repos.Where(r => Directory.Exists(r.Path)).ToList();
        foreach (var r in existing) OpenRequested?.Invoke(r);
        Status = existing.Count == repos.Count
            ? $"Abiertos {existing.Count} repositorios"
            : $"Abiertos {existing.Count} de {repos.Count} (algunas carpetas ya no existen)";
    }

    [RelayCommand]
    private Task NewGroup() => MoveToNewGroup(Array.Empty<RepoEntry>());

    private async Task MoveToNewGroup(IReadOnlyList<RepoEntry> repos)
    {
        var name = await _dialogs.Prompt("Nuevo grupo", "Nombre del grupo", RepoGrouping.UniqueName(State, "Grupo"));
        if (string.IsNullOrWhiteSpace(name)) return;
        var group = RepoGrouping.Ensure(State, name);
        group.Collapsed = false;
        MoveTo(repos, group);
    }

    private void MoveTo(IReadOnlyList<RepoEntry> repos, RepoGroup? group)
    {
        RepoGrouping.Move(State, repos, group);
        _store.Save();
        Reload();
        if (repos.Count > 0)
            Status = group is null
                ? $"{Plural(repos.Count)} sin grupo"
                : $"{Plural(repos.Count)} en el grupo \"{group.Name}\"";
    }

    private async Task RenameGroup(RepoGroup group)
    {
        var name = await _dialogs.Prompt("Renombrar grupo", "Nuevo nombre", group.Name);
        if (string.IsNullOrWhiteSpace(name) || name.Trim() == group.Name) return;
        if (!RepoGrouping.Rename(State, group, name))
        {
            await _dialogs.Message("Renombrar grupo", $"Ya existe un grupo llamado \"{name.Trim()}\".");
            return;
        }
        _store.Save();
        Reload();
    }

    private async Task DeleteGroup(RepoGroup group, bool removeRepos)
    {
        var count = RepoGrouping.Members(State, group).Count();
        var message = removeRepos
            ? $"Se quitará el grupo \"{group.Name}\" y sus {Plural(count)} de la lista.\nNo se borra nada del disco."
            : $"Se eliminará el grupo \"{group.Name}\". Sus {Plural(count)} pasarán a \"Sin grupo\".";
        if (count > 0 && !await _dialogs.Confirm("Eliminar grupo", message, "Eliminar")) return;
        RepoGrouping.Delete(State, group, removeRepos);
        _store.Save();
        Reload();
    }

    private static string Plural(int n) => n == 1 ? "1 repositorio" : $"{n} repositorios";

    // ---------------------------------------------------------------- menus

    public IReadOnlyList<MenuAction> MenuForGroup(GroupHeaderRow header)
    {
        var g = header.Group;
        var repos = RepoGrouping.Members(State, g).ToList();
        var items = new List<MenuAction>
        {
            MenuAction.Sync($"Abrir todos ({repos.Count})", () => OpenAll(repos)),
            MenuAction.Sync(header.IsCollapsed ? "Expandir" : "Contraer", () => ToggleGroup(g)),
        };
        if (g is not null)
        {
            items.Add(MenuAction.Separator);
            items.Add(new MenuAction("Renombrar grupo…", () => RenameGroup(g)));
            items.Add(new MenuAction("Eliminar grupo (los repositorios quedan sin grupo)…", () => DeleteGroup(g, removeRepos: false)));
            items.Add(new MenuAction("Quitar el grupo y sus repositorios de la lista…", () => DeleteGroup(g, removeRepos: true)));
        }
        return MenuList.Clean(items);
    }

    public IReadOnlyList<MenuAction> MenuForRepos(IReadOnlyList<RepoEntry> repos)
    {
        if (repos.Count == 0) return Array.Empty<MenuAction>();
        var single = repos.Count == 1 ? repos[0] : null;
        var items = new List<MenuAction>
        {
            single is not null
                ? MenuAction.Sync("Abrir", () => Open(single))
                : MenuAction.Sync($"Abrir los {repos.Count}", () => OpenAll(repos)),
        };
        if (single is not null) items.Add(MenuAction.Sync("Favorito (alternar)", () => ToggleFavorite(single)));

        items.Add(MenuAction.Separator);
        var targets = new List<MenuAction>();
        foreach (var g in State.Groups)
        {
            var group = g;
            if (repos.All(r => RepoGrouping.Find(State, r.Group) == group)) continue;
            targets.Add(MenuAction.Sync(group.Name, () => MoveTo(repos, group)));
        }
        if (targets.Count > 0) targets.Add(MenuAction.Separator);
        targets.Add(new MenuAction("Nuevo grupo…", () => MoveToNewGroup(repos)));
        items.Add(MenuAction.Submenu(repos.Count == 1 ? "Mover a grupo" : $"Mover los {repos.Count} a grupo", targets));
        if (repos.Any(r => RepoGrouping.Find(State, r.Group) is not null))
            items.Add(MenuAction.Sync("Quitar del grupo", () => MoveTo(repos, null)));

        if (single is not null)
        {
            items.Add(MenuAction.Separator);
            items.Add(MenuAction.Sync("Abrir en el Explorador", () => OpenInExplorer(single)));
            items.Add(MenuAction.Sync("Abrir terminal aquí", () => OpenInTerminal(single)));
        }
        items.Add(MenuAction.Separator);
        items.Add(MenuAction.Sync(single is not null ? "Quitar de la lista" : $"Quitar los {repos.Count} de la lista", () => RemoveMany(repos)));
        return MenuList.Clean(items);
    }

    // ---------------------------------------------------------------- repositories

    [RelayCommand]
    private void Open(RepoEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;
        if (!Directory.Exists(entry.Path))
        {
            Status = $"La carpeta ya no existe: {entry.Path}";
            return;
        }
        OpenRequested?.Invoke(entry);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var dir = await _dialogs.PickFolder("Seleccionar repositorio");
        if (dir is null) return;
        await AddPathAsync(dir, open: true);
    }

    /// <summary>Adds a repository; a folder that only contains repositories is imported as a group.</summary>
    public async Task AddPathAsync(string dir, bool open)
    {
        var root = await _git.FindRoot(dir);
        if (root is null)
        {
            if (Directory.Exists(dir)) { await ImportFolderAsGroupAsync(dir); return; }
            Status = $"No es un repositorio git: {dir}";
            return;
        }
        var entry = _store.Add(root);
        Reload();
        SelectedRow = Rows.OfType<RepoRow>().FirstOrDefault(r => r.Entry == entry);
        Status = $"Agregado: {entry.Name}";
        _ = RefreshBranchesAsync();
        if (open) OpenRequested?.Invoke(entry);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        var dir = await _dialogs.PickFolder("Carpeta a escanear");
        if (dir is not null) await ImportFolderAsGroupAsync(dir);
    }

    /// <summary>
    /// Finds the repositories under <paramref name="dir"/> and puts them in a group named after the
    /// folder. Repositories already in another group stay where they are.
    /// </summary>
    private async Task ImportFolderAsGroupAsync(string dir)
    {
        Busy = true;
        Status = "Escaneando...";
        try
        {
            var found = await Task.Run(() => FindRepos(dir, 5));
            if (found.Count == 0)
            {
                Status = $"No se encontraron repositorios en {dir}";
                return;
            }
            var name = Path.GetFileName(dir.TrimEnd('\\', '/'));
            var group = RepoGrouping.Ensure(State, string.IsNullOrEmpty(name) ? dir : name);
            group.Collapsed = false;

            int added = 0, kept = 0;
            foreach (var f in found)
            {
                var entry = _store.Find(f);
                if (entry is null) { entry = _store.Add(f); added++; }
                if (RepoGrouping.Find(State, entry.Group) is { } other && other != group) { kept++; continue; }
                entry.Group = group.Name;
            }
            _store.Save();
            Reload();
            _ = RefreshBranchesAsync();
            Status = $"Grupo \"{group.Name}\": {Plural(found.Count)} encontrados, {added} nuevos" +
                     (kept > 0 ? $", {kept} ya estaban en otro grupo" : "") + ".";
        }
        finally { Busy = false; }
    }

    private static List<string> FindRepos(string root, int maxDepth)
    {
        var result = new List<string>();
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "node_modules", "bin", "obj", ".vs", "$RECYCLE.BIN" };
        void Walk(string dir, int depth)
        {
            if (File.Exists(Path.Combine(dir, ".git")) || Directory.Exists(Path.Combine(dir, ".git")))
            {
                result.Add(dir);
                return; // don't descend into repos
            }
            if (depth >= maxDepth) return;
            try
            {
                foreach (var sub in Directory.EnumerateDirectories(dir))
                {
                    var name = Path.GetFileName(sub);
                    if (skip.Contains(name) || name.StartsWith('.')) continue;
                    Walk(sub, depth + 1);
                }
            }
            catch { /* inaccessible */ }
        }
        Walk(root, 0);
        return result;
    }

    [RelayCommand]
    private async Task CloneAsync()
    {
        var f = await _dialogs.Form("Clonar repositorio", new[] { ("URL del repositorio", ""), ("Carpeta de destino", "") }, "Clonar");
        if (f is null || string.IsNullOrWhiteSpace(f[0])) return;
        var url = f[0].Trim();
        var dest = f[1].Trim();
        if (dest.Length == 0)
        {
            var parent = await _dialogs.PickFolder("Carpeta donde clonar");
            if (parent is null) return;
            var name = Path.GetFileNameWithoutExtension(url.TrimEnd('/', '\\'));
            dest = Path.Combine(parent, name);
        }
        Busy = true;
        Status = "Clonando...";
        try
        {
            var res = await _git.Clone(url, dest, chunk =>
            {
                var last = chunk.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (last is not null) Avalonia.Threading.Dispatcher.UIThread.Post(() => Status = last.Trim());
            }, CancellationToken.None);
            if (!res.Ok)
            {
                Status = "Clonado falló";
                await _dialogs.Message("Error al clonar", res.Message);
                return;
            }
            await AddPathAsync(dest, open: true);
        }
        finally { Busy = false; }
    }

    public void RemoveMany(IReadOnlyList<RepoEntry> repos)
    {
        foreach (var r in repos) _store.Remove(r);
        Reload();
        Status = repos.Count == 1
            ? $"Quitado de la lista: {repos[0].Name} (no se borró nada del disco)"
            : $"Quitados {repos.Count} repositorios de la lista (no se borró nada del disco)";
    }

    [RelayCommand]
    private void ToggleFavorite(RepoEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;
        entry.Favorite = !entry.Favorite;
        _store.Save();
        Reload();
    }

    [RelayCommand]
    private void OpenInExplorer(RepoEntry? entry)
    {
        entry ??= Selected;
        if (entry is not null) _shell.OpenFolder(entry.Path);
    }

    [RelayCommand]
    private void OpenInTerminal(RepoEntry? entry)
    {
        entry ??= Selected;
        if (entry is not null) _shell.OpenTerminal(entry.Path);
    }
}
