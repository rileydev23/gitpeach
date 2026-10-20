using GitPeach.Services;

namespace GitPeach.Tests;

public class RepoGroupingTests
{
    private static AppState State(params (string Name, string? Group)[] repos)
    {
        var s = new AppState();
        foreach (var (name, group) in repos)
        {
            s.Repos.Add(new RepoEntry { Name = name, Path = @"C:\r\" + name, Group = group });
            if (group is not null) RepoGrouping.Ensure(s, group);
        }
        return s;
    }

    [Fact]
    public void Ensure_is_case_insensitive_and_unique_name_adds_a_suffix()
    {
        var s = new AppState();
        var a = RepoGrouping.Ensure(s, "Trabajo");
        Assert.Same(a, RepoGrouping.Ensure(s, "trabajo"));
        Assert.Single(s.Groups);
        Assert.Equal("Trabajo (2)", RepoGrouping.UniqueName(s, "Trabajo"));
        Assert.Equal("Otro", RepoGrouping.UniqueName(s, "Otro"));
    }

    [Fact]
    public void Build_lists_groups_in_order_then_ungrouped()
    {
        var s = State(("a", "B-grupo"), ("b", null), ("c", "A-grupo"), ("d", "B-grupo"));
        s.Repos[3].Favorite = true;

        var sections = RepoGrouping.Build(s, _ => true, hideEmpty: false);

        Assert.Equal(new[] { "B-grupo", "A-grupo", null }, sections.Select(x => x.Group?.Name));
        Assert.Equal(new[] { "d", "a" }, sections[0].Repos.Select(r => r.Name)); // favorites first
        Assert.Equal(new[] { "b" }, sections[2].Repos.Select(r => r.Name));
    }

    [Fact]
    public void Empty_groups_are_hidden_only_when_asked()
    {
        var s = State(("a", null));
        RepoGrouping.Ensure(s, "Vacío");
        Assert.Contains(RepoGrouping.Build(s, _ => true, hideEmpty: false), x => x.Group?.Name == "Vacío");
        Assert.DoesNotContain(RepoGrouping.Build(s, _ => true, hideEmpty: true), x => x.Group?.Name == "Vacío");
    }

    [Fact]
    public void Repos_pointing_to_a_missing_group_count_as_ungrouped()
    {
        var s = State(("a", null));
        s.Repos[0].Group = "ya no existe";
        var section = Assert.Single(RepoGrouping.Build(s, _ => true, hideEmpty: true));
        Assert.Null(section.Group);
    }

    [Fact]
    public void Rename_moves_members_and_refuses_duplicates()
    {
        var s = State(("a", "Uno"), ("b", "Dos"));
        var uno = RepoGrouping.Find(s, "Uno")!;

        Assert.False(RepoGrouping.Rename(s, uno, "dos"));
        Assert.True(RepoGrouping.Rename(s, uno, "Primero"));
        Assert.Equal("Primero", s.Repos[0].Group);
        Assert.True(RepoGrouping.Rename(s, uno, "PRIMERO")); // only the case changes
    }

    [Fact]
    public void Delete_ungroups_or_removes_members()
    {
        var s = State(("a", "G"), ("b", "G"), ("c", null));
        s.OpenTabs.Add(@"C:\r\a");

        RepoGrouping.Delete(s, RepoGrouping.Find(s, "G")!, removeRepos: false);
        Assert.Empty(s.Groups);
        Assert.All(s.Repos, r => Assert.Null(r.Group));

        var g = RepoGrouping.Ensure(s, "H");
        RepoGrouping.Move(s, s.Repos.Take(2).ToList(), g);
        RepoGrouping.Delete(s, g, removeRepos: true);
        Assert.Equal(new[] { "c" }, s.Repos.Select(r => r.Name));
        Assert.Empty(s.OpenTabs);
    }
}
