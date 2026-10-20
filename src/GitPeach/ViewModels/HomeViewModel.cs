using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GitPeach.Git;
using GitPeach.Services;
using GitPeach.Views;

namespace GitPeach.ViewModels;

public partial class HomeViewModel : TabViewModel
{
    private readonly RepoStore _store;
    private readonly Action<RepoEntry> _open;

    public override bool Closable => false;
    public ObservableCollection<RepoEntry> Repos { get; } = new();

    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private bool _onlyFavorites;
    [ObservableProperty] private RepoEntry? _selected;
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private bool _isEmpty;

    public HomeViewModel(RepoStore store, Action<RepoEntry> open)
    {
        _store = store;
        _open = open;
        Title = "Repositorios";
        Reload();
        _ = RefreshBranchesAsync();
    }

    partial void OnFilterChanged(string value) => Reload();
    partial void OnOnlyFavoritesChanged(bool value) => Reload();

    public void Reload()
    {
        var sel = Selected;
        Repos.Clear();
        var q = Filter.Trim();
        foreach (var r in _store.State.Repos
                     .Where(r => (!OnlyFavorites || r.Favorite) && Dialogs.Fuzzy(r.Name + " " + r.Path, q))
                     .OrderByDescending(r => r.Favorite)
                     .ThenByDescending(r => r.LastOpened))
            Repos.Add(r);
        Selected = sel is not null && Repos.Contains(sel) ? sel : null;
        IsEmpty = Repos.Count == 0;
    }

    public async Task RefreshBranchesAsync()
    {
        var snapshot = _store.State.Repos.ToList();
        await Task.WhenAll(snapshot.Select(async r =>
        {
            r.Missing = !Directory.Exists(r.Path);
            r.Branch = r.Missing ? null : await GitRepository.CurrentBranchOf(r.Path);
        }));
        Reload();
    }

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
        _open(entry);
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var dir = await Dialogs.PickFolder("Seleccionar repositorio");
        if (dir is null) return;
        await AddPathAsync(dir, open: true);
    }

    public async Task AddPathAsync(string dir, bool open)
    {
        var root = await GitRepository.FindRoot(dir);
        if (root is null)
        {
            Status = $"No es un repositorio git: {dir}";
            return;
        }
        var entry = _store.Add(root);
        Reload();
        Selected = entry;
        Status = $"Agregado: {entry.Name}";
        _ = RefreshBranchesAsync();
        if (open) _open(entry);
    }

    [RelayCommand]
    private async Task ScanAsync()
    {
        var dir = await Dialogs.PickFolder("Carpeta a escanear");
        if (dir is null) return;
        Busy = true;
        Status = "Escaneando...";
        try
        {
            var found = await Task.Run(() => FindRepos(dir, 5));
            int added = 0;
            foreach (var f in found)
            {
                if (_store.Find(f) is null) { _store.Add(f); added++; }
            }
            Reload();
            _ = RefreshBranchesAsync();
            Status = $"Escaneo terminado: {found.Count} repositorios encontrados, {added} nuevos.";
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
        var f = await Dialogs.Form("Clonar repositorio", new[] { ("URL del repositorio", ""), ("Carpeta de destino", "") }, "Clonar");
        if (f is null || string.IsNullOrWhiteSpace(f[0])) return;
        var url = f[0].Trim();
        var dest = f[1].Trim();
        if (dest.Length == 0)
        {
            var parent = await Dialogs.PickFolder("Carpeta donde clonar");
            if (parent is null) return;
            var name = Path.GetFileNameWithoutExtension(url.TrimEnd('/', '\\'));
            dest = Path.Combine(parent, name);
        }
        Busy = true;
        Status = "Clonando...";
        try
        {
            var res = await GitRepository.Clone(url, dest, chunk =>
            {
                var last = chunk.Split('\r', '\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                if (last is not null) Avalonia.Threading.Dispatcher.UIThread.Post(() => Status = last.Trim());
            }, CancellationToken.None);
            if (!res.Ok)
            {
                Status = "Clonado falló";
                await Dialogs.Message("Error al clonar", res.Message);
                return;
            }
            await AddPathAsync(dest, open: true);
        }
        finally { Busy = false; }
    }

    [RelayCommand]
    private void Remove(RepoEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;
        _store.Remove(entry);
        Reload();
        Status = $"Quitado de la lista: {entry.Name} (no se borró nada del disco)";
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
        if (entry is null) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{entry.Path}\"") { UseShellExecute = true }); } catch { }
    }

    [RelayCommand]
    private void OpenInTerminal(RepoEntry? entry)
    {
        entry ??= Selected;
        if (entry is null) return;
        try
        {
            var psi = new ProcessStartInfo("cmd.exe") { WorkingDirectory = entry.Path, UseShellExecute = true };
            Process.Start(psi);
        }
        catch { }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await RefreshBranchesAsync();
}
