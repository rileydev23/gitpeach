using System.Collections.ObjectModel;
using GitPeach.Models;
using GitPeach.Services;

namespace GitPeach.ViewModels;

/// <summary>Branches, remotes, tags and stashes, with their context actions.</summary>
public sealed class SidebarViewModel(RepoActions actions, IClipboardService clipboard)
{
    public ObservableCollection<SidebarItem> Items { get; } = new();

    /// <summary>Asks the commit list to select the commit a ref points to.</summary>
    public event Action<string>? SelectHashRequested;

    public void Build(RepoSnapshot snap)
    {
        var refs = snap.Refs;
        Items.Clear();
        Items.Add(new SidebarItem { Kind = SidebarKind.Header, Text = Loc.T("side.branches") });
        foreach (var b in refs.Locals.OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
            Items.Add(new SidebarItem
            {
                Kind = SidebarKind.LocalBranch, Text = b.Name, IsCurrent = b.IsCurrent, Indent = 12, Payload = b,
                Detail = (b.IsCurrent ? "● " : "") + (b.Track ?? "").Trim('[', ']').Replace("ahead ", "↑").Replace("behind ", "↓").Replace(", ", " "),
            });

        Items.Add(new SidebarItem { Kind = SidebarKind.Header, Text = Loc.T("side.remotes") });
        foreach (var remote in refs.RemoteNames)
        {
            Items.Add(new SidebarItem { Kind = SidebarKind.Header, Text = remote, Indent = 12 });
            foreach (var b in refs.Remotes.Where(b => b.Remote == remote).OrderBy(b => b.Name, StringComparer.OrdinalIgnoreCase))
                Items.Add(new SidebarItem { Kind = SidebarKind.RemoteBranch, Text = b.ShortRemoteName, Indent = 24, Payload = b });
        }

        if (refs.Tags.Count > 0)
        {
            Items.Add(new SidebarItem { Kind = SidebarKind.Header, Text = "TAGS" });
            foreach (var t in refs.Tags.OrderByDescending(t => t.Name, StringComparer.OrdinalIgnoreCase))
                Items.Add(new SidebarItem { Kind = SidebarKind.Tag, Text = t.Name, Indent = 12, Payload = t });
        }

        if (snap.Stashes.Count > 0)
        {
            Items.Add(new SidebarItem { Kind = SidebarKind.Header, Text = "STASHES" });
            foreach (var s in snap.Stashes)
                Items.Add(new SidebarItem { Kind = SidebarKind.Stash, Text = s.Message, Detail = s.Name, Indent = 12, Payload = s });
        }
    }

    /// <summary>Double-click: checkout branches, otherwise jump to the commit.</summary>
    public async Task ActivateAsync(SidebarItem item)
    {
        switch (item.Payload)
        {
            case BranchInfo { IsRemote: false, IsCurrent: false } b:
                await actions.Checkout(b.Name);
                break;
            case BranchInfo { IsRemote: true } rb:
                await actions.CheckoutRemote(rb);
                break;
            case BranchInfo b:
                SelectHashRequested?.Invoke(b.Hash);
                break;
            case TagInfo t:
                SelectHashRequested?.Invoke(t.Hash);
                break;
        }
    }

    public IReadOnlyList<MenuAction> MenuFor(SidebarItem item)
    {
        var list = new List<MenuAction>();
        switch (item.Payload)
        {
            case BranchInfo { IsRemote: false } b:
                if (!b.IsCurrent)
                {
                    list.Add(new("Checkout", () => actions.Checkout(b.Name)));
                    list.Add(new(Loc.T("side.menu.merge"), () => actions.Merge(b.Name)));
                    list.Add(new(Loc.T("side.menu.rebase"), () => actions.Rebase(b.Name)));
                    list.Add(MenuAction.Separator);
                }
                list.Add(MenuAction.Sync(Loc.T("side.menu.goTo"), () => SelectHashRequested?.Invoke(b.Hash)));
                list.Add(new(Loc.T("side.menu.branchFrom"), () => actions.NewBranch(b.Name)));
                list.Add(new(Loc.T("side.menu.rename"), () => actions.RenameBranch(b)));
                list.Add(new(Loc.T("side.menu.copyName"), () => clipboard.SetTextAsync(b.Name)));
                list.Add(MenuAction.Separator);
                list.Add(new("Push", () => actions.PushBranch(b)));
                if (!b.IsCurrent)
                {
                    list.Add(MenuAction.Separator);
                    list.Add(new(Loc.T("side.menu.deleteBranch"), () => actions.DeleteBranch(b)));
                }
                break;
            case BranchInfo { IsRemote: true } rb:
                list.Add(new(Loc.T("side.menu.checkoutRemote"), () => actions.CheckoutRemote(rb)));
                list.Add(new(Loc.T("side.menu.merge"), () => actions.Merge(rb.Name)));
                list.Add(MenuAction.Sync(Loc.T("side.menu.goTo"), () => SelectHashRequested?.Invoke(rb.Hash)));
                list.Add(new(Loc.T("side.menu.branchFrom"), () => actions.NewBranch(rb.Name)));
                list.Add(MenuAction.Separator);
                list.Add(new(Loc.T("side.menu.deleteRemoteBranch"), () => actions.DeleteRemoteBranch(rb)));
                break;
            case TagInfo t:
                list.Add(new("Checkout", () => actions.Checkout(t.Name)));
                list.Add(MenuAction.Sync(Loc.T("side.menu.goTo"), () => SelectHashRequested?.Invoke(t.Hash)));
                list.Add(new(Loc.T("side.menu.copyName"), () => clipboard.SetTextAsync(t.Name)));
                list.Add(MenuAction.Separator);
                list.Add(new(Loc.T("side.menu.pushTag"), () => actions.PushTag(t)));
                list.Add(new(Loc.T("side.menu.deleteTag"), () => actions.DeleteTag(t)));
                list.Add(new(Loc.T("side.menu.deleteRemoteTag"), () => actions.DeleteRemoteTag(t)));
                break;
            case StashInfo s:
                list.Add(new("Pop", () => actions.StashPop(s)));
                list.Add(new("Apply", () => actions.StashApply(s)));
                list.Add(MenuAction.Separator);
                list.Add(new("Drop…", () => actions.StashDrop(s)));
                break;
        }
        return list;
    }
}
