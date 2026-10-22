namespace GitPeach.Services;

/// <summary>One section of the repository list: a group (or the ungrouped repos, when <see cref="Group"/> is null).</summary>
public sealed record RepoSection(RepoGroup? Group, IReadOnlyList<RepoEntry> Repos);

/// <summary>Group operations over <see cref="AppState"/>. Pure: the caller decides when to save.</summary>
public static class RepoGrouping
{
    private static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;

    public static RepoGroup? Find(AppState s, string? name)
        => name is null ? null : s.Groups.FirstOrDefault(g => Cmp.Equals(g.Name, name));

    /// <summary>The group with that name (case-insensitive), created at the end if missing.</summary>
    public static RepoGroup Ensure(AppState s, string name)
    {
        name = name.Trim();
        if (Find(s, name) is { } g) return g;
        g = new RepoGroup { Name = name };
        s.Groups.Add(g);
        return g;
    }

    /// <summary>"name", or "name (2)", "name (3)"… when taken.</summary>
    public static string UniqueName(AppState s, string baseName)
    {
        baseName = string.IsNullOrWhiteSpace(baseName) ? Loc.T("home.newGroup.default") : baseName.Trim();
        var name = baseName;
        for (int i = 2; Find(s, name) is not null; i++) name = $"{baseName} ({i})";
        return name;
    }

    public static void Move(AppState s, IEnumerable<RepoEntry> repos, RepoGroup? group)
    {
        foreach (var r in repos) r.Group = group?.Name;
    }

    /// <summary>Renames a group and its members. False when another group already has that name.</summary>
    public static bool Rename(AppState s, RepoGroup group, string newName)
    {
        newName = newName.Trim();
        if (newName.Length == 0) return false;
        if (Find(s, newName) is { } other && !ReferenceEquals(other, group)) return false;
        foreach (var r in Members(s, group)) r.Group = newName;
        group.Name = newName;
        return true;
    }

    /// <summary>Deletes the group; its repositories become ungrouped (or are removed from the list).</summary>
    public static void Delete(AppState s, RepoGroup group, bool removeRepos)
    {
        var members = Members(s, group).ToList();
        if (removeRepos)
        {
            foreach (var r in members)
            {
                s.Repos.Remove(r);
                s.OpenTabs.RemoveAll(t => Cmp.Equals(t, r.Path));
            }
        }
        else Move(s, members, null);
        s.Groups.Remove(group);
    }

    public static IEnumerable<RepoEntry> Members(AppState s, RepoGroup? group)
        => group is null
            ? s.Repos.Where(r => Find(s, r.Group) is null)   // unknown group names count as ungrouped
            : s.Repos.Where(r => Cmp.Equals(r.Group, group.Name));

    /// <summary>
    /// Sections in display order: groups as stored, then the ungrouped repos. Repos are sorted by
    /// favorite, then most recently opened. Empty groups are kept unless <paramref name="hideEmpty"/>.
    /// </summary>
    public static List<RepoSection> Build(AppState s, Func<RepoEntry, bool> include, bool hideEmpty)
    {
        IReadOnlyList<RepoEntry> Sorted(IEnumerable<RepoEntry> repos) => repos.Where(include)
            .OrderByDescending(r => r.Favorite).ThenByDescending(r => r.LastOpened).ToList();

        var sections = new List<RepoSection>();
        foreach (var g in s.Groups)
        {
            var repos = Sorted(Members(s, g));
            if (repos.Count > 0 || !hideEmpty) sections.Add(new RepoSection(g, repos));
        }
        var ungrouped = Sorted(Members(s, null));
        if (ungrouped.Count > 0) sections.Add(new RepoSection(null, ungrouped));
        return sections;
    }
}
