using System.Text.Json;
using System.Text.Json.Serialization;

namespace GitPeach.Services;

public sealed class RepoEntry
{
    public string Path { get; set; } = "";
    public string Name { get; set; } = "";
    public bool Favorite { get; set; }
    public DateTimeOffset LastOpened { get; set; }
    /// <summary>Name of the <see cref="RepoGroup"/> it belongs to; null = ungrouped.</summary>
    public string? Group { get; set; }
    [JsonIgnore] public string? Branch { get; set; }
    [JsonIgnore] public bool Missing { get; set; }
}

public sealed class RepoGroup
{
    public string Name { get; set; } = "";
    public bool Collapsed { get; set; }
}

public sealed class AppState
{
    public List<RepoEntry> Repos { get; set; } = new();
    /// <summary>Groups in display order.</summary>
    public List<RepoGroup> Groups { get; set; } = new();
    public bool UngroupedCollapsed { get; set; }
    public List<string> OpenTabs { get; set; } = new();
    public string? ActiveTab { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public int? X { get; set; }
    public int? Y { get; set; }
    public bool Maximized { get; set; }
    public string? GitPath { get; set; }
    /// <summary>Interface language: "en", "es" or null to follow the system.</summary>
    public string? Language { get; set; }
    /// <summary>Look for a new version on GitHub at most once a day.</summary>
    public bool CheckUpdates { get; set; } = true;
    public DateTimeOffset? LastUpdateCheck { get; set; }
    /// <summary>Newest version seen by the last check ("1.2.3"), so the button shows without asking GitHub again.</summary>
    public string? LatestVersion { get; set; }
    /// <summary>Version the user chose to skip: no button until a newer one comes out.</summary>
    public string? SkippedVersion { get; set; }
}

[JsonSerializable(typeof(AppState))]
[JsonSourceGenerationOptions(WriteIndented = true)]
internal sealed partial class AppStateContext : JsonSerializerContext { }

public sealed class RepoStore : IAppStateStore
{
    private readonly string _file;

    public string DataFolder => System.IO.Path.GetDirectoryName(_file)!;
    private readonly object _lock = new();

    public AppState State { get; private set; } = new();

    public RepoStore()
    {
        var dir = PeachApps.SettingsFolder("GitPeach");
        Directory.CreateDirectory(dir);
        _file = System.IO.Path.Combine(dir, "state.json");
        Load();
    }

    private void Load()
    {
        try
        {
            if (File.Exists(_file))
                State = JsonSerializer.Deserialize(File.ReadAllText(_file), AppStateContext.Default.AppState) ?? new AppState();
        }
        catch
        {
            State = new AppState();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                var tmp = _file + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(State, AppStateContext.Default.AppState));
                File.Move(tmp, _file, true);
            }
            catch { /* best effort */ }
        }
    }

    public static string Normalize(string p) => System.IO.Path.GetFullPath(p).TrimEnd('\\', '/');

    public RepoEntry? Find(string path)
        => State.Repos.FirstOrDefault(r => string.Equals(Normalize(r.Path), Normalize(path), StringComparison.OrdinalIgnoreCase));

    public RepoEntry Add(string path)
    {
        var existing = Find(path);
        if (existing is not null) return existing;
        var norm = Normalize(path);
        var e = new RepoEntry { Path = norm, Name = System.IO.Path.GetFileName(norm), LastOpened = DateTimeOffset.Now };
        State.Repos.Add(e);
        Save();
        return e;
    }

    public void Remove(RepoEntry e)
    {
        State.Repos.Remove(e);
        State.OpenTabs.RemoveAll(t => string.Equals(t, e.Path, StringComparison.OrdinalIgnoreCase));
        Save();
    }

    public void Touch(RepoEntry e)
    {
        e.LastOpened = DateTimeOffset.Now;
        Save();
    }
}
